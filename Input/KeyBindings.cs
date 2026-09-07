using System;
using System.Linq;
using System.Windows.Forms;
using Rage;
using UltimateBackupReplacement.Commands;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.UI;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Input
{
public static class KeyBindings
{
private const Keys OpenTabletKey = Keys.B;
private const Keys GetInVehiclesKey = Keys.T;
private const Keys ForceArrestKey = Keys.O;
private const Keys OpenFireKey = Keys.U;

    private const int HoldThresholdMs = 350;

    private static bool _uHeld;
    private static int _uPressStart;

    private static bool _prevB;
    private static bool _prevT;
    private static bool _prevO;
    private static bool _prevEscape;

    public static void Tick()
    {
        bool bDown = Game.IsKeyDownRightNow(OpenTabletKey);
        bool tDown = Game.IsKeyDownRightNow(GetInVehiclesKey);
        bool oDown = Game.IsKeyDownRightNow(ForceArrestKey);
        bool escDown = Game.IsKeyDownRightNow(Keys.Escape);

        bool bJustPressed = bDown && !_prevB;
        bool tJustPressed = tDown && !_prevT;
        bool oJustPressed = oDown && !_prevO;
        bool escJustPressed = escDown && !_prevEscape;

        _prevB = bDown;
        _prevT = tDown;
        _prevO = oDown;
        _prevEscape = escDown;

        if (bJustPressed)
            NativeMenu.ToggleMainMenu();

        HandleGetInVehicles(tJustPressed, escJustPressed);
        HandleForceArrest(oJustPressed);
        HandleOpenFire();
    }

    private static void HandleGetInVehicles(
        bool tJustPressed,
        bool escJustPressed)
    {
        if (NativeMenu.IsInTargetingMode)
        {
            if (tJustPressed)
                NativeMenu.ConfirmTarget(
                    TargetingHelper.GetAimPoint());
            else if (escJustPressed)
                NativeMenu.CancelTargeting();

            return;
        }

        if (NativeMenu.IsOpen)
            return;

        if (!tJustPressed)
            return;

        var player = Game.LocalPlayer.Character;

        bool playerIsDriving =
            player.IsInVehicle(null, false) &&
            player.CurrentVehicle != null &&
            player.CurrentVehicle.Driver == player;

        if (!playerIsDriving)
            return;

        foreach (var unit in UnitManager.Partners.Where(
            u => u.AssignedVehicle != null &&
                 u.Status != UnitStatus.InVehicle))
        {
            MovementDispatcher.GetInVehicle(unit);
        }
    }

    private static void HandleForceArrest(bool oJustPressed)
    {
        if (!oJustPressed || NativeMenu.IsOpen)
            return;

        var target = TargetingHelper.GetAimedOrNearestPed();

        if (target == null)
            return;

        var available = UnitManager.Partners
            .Where(
                u => u.IsAlive &&
                     (u.State == UnitState.Idle ||
                      u.State == UnitState.Following))
            .ToList();

        if (available.Count == 0)
            return;

        ArrestDispatcher.ForceArrest(
            available,
            target);
    }

    private static void HandleOpenFire()
    {
        if (NativeMenu.IsOpen)
            return;

        bool down =
            Game.IsKeyDownRightNow(OpenFireKey);

        if (down && !_uHeld)
        {
            _uHeld = true;
            _uPressStart = Environment.TickCount;
        }
        else if (!down && _uHeld)
        {
            _uHeld = false;

            int heldFor =
                Environment.TickCount - _uPressStart;

            var aimPoint =
                TargetingHelper.GetAimPoint();

            var partners =
                UnitManager.Partners.Where(
                    u => u.IsAlive);

            if (heldFor < HoldThresholdMs)
            {
                CombatDispatcher.OpenFireSingle(
                    partners,
                    aimPoint);
            }
            else
            {
                CombatDispatcher.OpenFireAll(
                    partners,
                    aimPoint);
            }
        }
    }
}

}
