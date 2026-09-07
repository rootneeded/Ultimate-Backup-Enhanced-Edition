using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.Elements;
using UltimateBackupReplacement.Commands;
using UltimateBackupReplacement.Compatibility;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.UI
{
public static class NativeMenu
{
private static readonly MenuPool Pool = new MenuPool();

    private static UIMenu _mainMenu;
    private static UIMenu _partnersMenu;
    private static UIMenu _code2Menu;
    private static UIMenu _code3Menu;

    private static bool _initialized;
    private static bool _wasAnyMenuOpen;

    public static bool IsInTargetingMode { get; private set; }
    private static Action<Vector3> _pendingTargetAction;

    private static readonly Dictionary<UIMenuCheckboxItem, Unit> PartnerCheckboxes =
        new Dictionary<UIMenuCheckboxItem, Unit>();

    private static readonly Dictionary<UIMenuCheckboxItem, Unit> Code2Checkboxes =
        new Dictionary<UIMenuCheckboxItem, Unit>();

    private static readonly Dictionary<UIMenuCheckboxItem, Unit> Code3Checkboxes =
        new Dictionary<UIMenuCheckboxItem, Unit>();

    private static readonly Dictionary<UIMenuItem, UnitType> Code2CallItems =
        new Dictionary<UIMenuItem, UnitType>();

    private static readonly Dictionary<UIMenuItem, UnitType> Code3CallItems =
        new Dictionary<UIMenuItem, UnitType>();

    private static UIMenuListItem _patrolItem;

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        BuildMenus();
    }

    private static void BuildMenus()
    {
        _mainMenu = new UIMenu("LSPD MDT", "~b~UNIT CONTROL");
        Pool.Add(_mainMenu);

        _partnersMenu = Pool.AddSubMenu(_mainMenu, "PARTNERS");
        _code2Menu = Pool.AddSubMenu(_mainMenu, "CODE 2");
        _code3Menu = Pool.AddSubMenu(_mainMenu, "CODE 3");

        _partnersMenu.OnMenuOpen += sender => RebuildPartnersMenu();

        _code2Menu.OnMenuOpen += sender =>
            RebuildCallMenu(
                _code2Menu,
                UnitSource.Code2,
                Code2Checkboxes,
                Code2CallItems);

        _code3Menu.OnMenuOpen += sender =>
            RebuildCallMenu(
                _code3Menu,
                UnitSource.Code3,
                Code3Checkboxes,
                Code3CallItems);

        _partnersMenu.OnItemSelect += OnPartnersItemSelect;

        _partnersMenu.OnCheckboxChange += (sender, item, isChecked) =>
        {
            Unit unit;

            if (PartnerCheckboxes.TryGetValue(item, out unit))
                unit.IsSelected = isChecked;
        };

        _partnersMenu.OnListChange += OnPartnersListChange;

        _code2Menu.OnItemSelect += (sender, item, index) =>
            OnCallMenuItemSelect(
                _code2Menu,
                UnitSource.Code2,
                item,
                Code2Checkboxes,
                Code2CallItems);

        _code2Menu.OnCheckboxChange += (sender, item, isChecked) =>
        {
            Unit unit;

            if (Code2Checkboxes.TryGetValue(item, out unit))
                unit.IsSelected = isChecked;
        };

        _code3Menu.OnItemSelect += (sender, item, index) =>
            OnCallMenuItemSelect(
                _code3Menu,
                UnitSource.Code3,
                item,
                Code3Checkboxes,
                Code3CallItems);

        _code3Menu.OnCheckboxChange += (sender, item, isChecked) =>
        {
            Unit unit;

            if (Code3Checkboxes.TryGetValue(item, out unit))
                unit.IsSelected = isChecked;
        };
    }

    public static void Tick()
    {
        EnsureInitialized();
        Pool.ProcessMenus();

        bool anyOpen = Pool.IsAnyMenuOpen();

        if (anyOpen != _wasAnyMenuOpen)
        {
            Game.IsPaused = anyOpen || IsInTargetingMode;
            _wasAnyMenuOpen = anyOpen;
        }
    }

    public static bool IsOpen
    {
        get { return Pool.IsAnyMenuOpen(); }
    }

    public static void ToggleMainMenu()
    {
        EnsureInitialized();
        _mainMenu.Visible = !_mainMenu.Visible;
    }

    public static void EnterTargetingMode(Action<Vector3> onConfirmed)
    {
        _pendingTargetAction = onConfirmed;
        IsInTargetingMode = true;

        _mainMenu.Visible = false;
        _partnersMenu.Visible = false;
        _code2Menu.Visible = false;
        _code3Menu.Visible = false;
    }

    public static void ConfirmTarget(Vector3 point)
    {
        if (!IsInTargetingMode)
            return;

        Action<Vector3> action = _pendingTargetAction;

        _pendingTargetAction = null;
        IsInTargetingMode = false;
        Game.IsPaused = false;

        if (action != null)
            action(point);
    }

    public static void CancelTargeting()
    {
        if (!IsInTargetingMode)
            return;

        _pendingTargetAction = null;
        IsInTargetingMode = false;
        Game.IsPaused = false;
    }

    private static void AddSection(UIMenu menu, string text)
    {
        menu.AddItem(new UIMenuItem("~b~" + text));
    }

    private static void RebuildPartnersMenu()
    {
        _partnersMenu.Clear();
        PartnerCheckboxes.Clear();

        _partnersMenu.AddItem(
            new UIMenuItem(
                "New Partner",
                "Spawn a partner with default loadout near you"));

        AddSection(_partnersMenu, "UNITS");

        foreach (var unit in UnitManager.Partners)
        {
            string idText = unit.Id.ToString();
            string shortId =
                idText.Length > 4
                    ? idText.Substring(0, 4)
                    : idText;

            string label =
                string.Format(
                    "{0} #{1} [{2}]",
                    unit.Type,
                    shortId,
                    unit.Status);

            var checkbox =
                new UIMenuCheckboxItem(
                    label,
                    unit.IsSelected,
                    unit.State.ToString());

            _partnersMenu.AddItem(checkbox);
            PartnerCheckboxes[checkbox] = unit;
        }

        _partnersMenu.AddItem(new UIMenuItem("Select All"));

        AddSection(_partnersMenu, "MOVEMENT");

        _partnersMenu.AddItem(
            new UIMenuItem(
                "Go To",
                "Aim at a point, press T to confirm"));

        _partnersMenu.AddItem(new UIMenuItem("Follow Me"));
        _partnersMenu.AddItem(new UIMenuItem("Convoy"));
        _partnersMenu.AddItem(new UIMenuItem("Get In Vehicle"));
        _partnersMenu.AddItem(new UIMenuItem("Exit Vehicle"));
        _partnersMenu.AddItem(new UIMenuItem("Regroup"));
        _partnersMenu.AddItem(new UIMenuItem("Hold Position"));
        _partnersMenu.AddItem(new UIMenuItem("Take Cover"));
        _partnersMenu.AddItem(new UIMenuItem("Cover Me"));

        _patrolItem =
            new UIMenuListItem(
                "Patrol",
                BuildRadiusOptions(),
                4);

        _partnersMenu.AddItem(_patrolItem);

        AddSection(_partnersMenu, "SUSPECT");

        _partnersMenu.AddItem(new UIMenuItem("Stop Ped"));
        _partnersMenu.AddItem(new UIMenuItem("Request ID"));
        _partnersMenu.AddItem(new UIMenuItem("Request Driver License"));
        _partnersMenu.AddItem(new UIMenuItem("Request Registration"));
        _partnersMenu.AddItem(new UIMenuItem("Request Exit Vehicle"));

        AddSection(_partnersMenu, "ARREST");

        _partnersMenu.AddItem(new UIMenuItem("Arrest"));
        _partnersMenu.AddItem(new UIMenuItem("Force Arrest"));
        _partnersMenu.AddItem(new UIMenuItem("Guard Suspect"));

        AddSection(_partnersMenu, "SEARCH");

        _partnersMenu.AddItem(new UIMenuItem("Search Ped"));
        _partnersMenu.AddItem(new UIMenuItem("Search Vehicle"));
    }

    private static List<dynamic> BuildRadiusOptions()
    {
        var list = new List<dynamic>();

        for (int r = 20; r <= 300; r += 20)
            list.Add(r + "m");

        return list;
    }

    private static float ParseRadiusIndex(int index)
    {
        int radius = (index + 1) * 20;

        if (radius < 20)
            radius = 20;

        if (radius > 300)
            radius = 300;

        return radius;
    }

    private static void OnPartnersListChange(
        UIMenu sender,
        UIMenuListItem item,
        int newIndex)
    {
        if (item != _patrolItem)
            return;

        float radius = ParseRadiusIndex(newIndex);

        MovementDispatcher.Patrol(
            UnitManager.Selected(UnitSource.Partner).ToList(),
            radius);
    }

    private static void OnPartnersItemSelect(
        UIMenu sender,
        UIMenuItem item,
        int index)
    {
        var selected =
            UnitManager.Selected(UnitSource.Partner).ToList();

        switch (item.Text)
        {
            case "New Partner":
            {
                var player = Game.LocalPlayer.Character;

                PartnerSpawner.Spawn(
                    new PartnerLoadout(),
                    player.Position + player.RightVector * 2.5f,
                    player.Heading);

                RebuildPartnersMenu();
                break;
            }

            case "Select All":
                UnitManager.SelectAll(UnitSource.Partner);
                RebuildPartnersMenu();
                break;

            case "Go To":
                if (selected.Count > 0)
                {
                    EnterTargetingMode(
                        point => MovementDispatcher.Go(selected, point));
                }
                break;

            case "Follow Me":
                MovementDispatcher.FollowMe(selected);
                break;

            case "Convoy":
                MovementDispatcher.Convoy(selected);
                break;

            case "Regroup":
                MovementDispatcher.Regroup(selected);
                break;

            case "Hold Position":
                MovementDispatcher.HoldPosition(selected);
                break;

            case "Take Cover":
                MovementDispatcher.TakeCover(selected);
                break;

            case "Cover Me":
                GuardDispatcher.CoverMe(selected);
                break;

            case "Get In Vehicle":
                foreach (var unit in selected)
                    MovementDispatcher.GetInVehicle(unit);
                break;

            case "Exit Vehicle":
                foreach (var unit in selected.Where(
                    u => u.Status == UnitStatus.InVehicle))
                {
                    MovementDispatcher.ExitVehicle(unit);
                }
                break;

            case "Stop Ped":
            {
                var unit = selected.FirstOrDefault();
                var target = TargetingHelper.GetAimedOrNearestPed();

                if (unit != null &&
                    TargetingHelper.IsValidSuspectTarget(target))
                {
                    unit.Ped.Tasks.FollowNavigationMeshToPosition(
                        target.Position,
                        target.Heading,
                        2f);
                }

                break;
            }

            case "Request ID":
                RunSuspectRequest(
                    selected,
                    t => LspdfrBridge.RequestPedId(t),
                    "REQUESTED_ID");
                break;

            case "Request Driver License":
                RunSuspectRequest(
                    selected,
                    t => LspdfrBridge.RequestDriverLicense(t),
                    "REQUESTED_LICENSE");
                break;

            case "Request Registration":
                RunSuspectRequest(
                    selected,
                    t => LspdfrBridge.RequestVehicleRegistration(t),
                    "REQUESTED_REGISTRATION");
                break;

            case "Request Exit Vehicle":
            {
                var target =
                    TargetingHelper.GetAimedOrNearestPed();

                if (TargetingHelper.IsValidSuspectTarget(target))
                {
                    target.Tasks.LeaveVehicle(
                        LeaveVehicleFlags.None);
                }

                break;
            }

            case "Arrest":
            {
                var unit = selected.FirstOrDefault();
                var target =
                    TargetingHelper.GetAimedOrNearestPed();

                if (unit != null &&
                    target != null)
                {
                    ArrestDispatcher.Arrest(unit, target);
                }

                break;
            }

            case "Force Arrest":
            {
                var target =
                    TargetingHelper.GetAimedOrNearestPed();

                if (selected.Count > 0 &&
                    target != null)
                {
                    ArrestDispatcher.ForceArrest(
                        selected,
                        target);
                }

                break;
            }

            case "Guard Suspect":
            {
                var unit = selected.FirstOrDefault();
                var target =
                    TargetingHelper.GetAimedOrNearestPed();

                if (unit != null &&
                    target != null)
                {
                    GuardDispatcher.GuardSuspect(
                        unit,
                        target);
                }

                break;
            }

            case "Search Ped":
            {
                var target =
                    TargetingHelper.GetAimedOrNearestPed();

                if (selected.Count > 0 &&
                    target != null)
                {
                    SearchDispatcher.SearchPed(
                        selected,
                        target);
                }

                break;
            }

            case "Search Vehicle":
            {
                var target =
                    TargetingHelper.GetAimedOrNearestVehicle();

                if (selected.Count > 0 &&
                    target != null)
                {
                    SearchDispatcher.SearchVehicle(
                        selected,
                        target);
                }

                break;
            }
        }
    }

    private static void RunSuspectRequest(
        List<Unit> units,
        Func<Ped, bool> lspdfrCall,
        string fallbackVoiceContext)
    {
        var unit = units.FirstOrDefault();
        var target =
            TargetingHelper.GetAimedOrNearestPed();

        if (unit == null ||
            !TargetingHelper.IsValidSuspectTarget(target))
            return;

        bool handled = lspdfrCall(target);

        if (!handled)
        {
            NativeFunction.Natives
                .PLAY_PED_AMBIENT_SPEECH_NATIVE(
                    unit.Ped,
                    fallbackVoiceContext,
                    "SPEECH_PARAMS_STANDARD");
        }
    }

    private static readonly Tuple<UnitType, string>[] CallableUnitTypes =
    {
        Tuple.Create(UnitType.LspdPatrol, "LSPD Patrol"),
        Tuple.Create(UnitType.LssdPatrol, "LSSD Patrol"),
        Tuple.Create(UnitType.StatePatrol, "State Patrol"),
        Tuple.Create(UnitType.Swat, "SWAT"),
        Tuple.Create(UnitType.Noose, "NOOSE"),
        Tuple.Create(UnitType.NooseSwat, "NOOSE SWAT"),
        Tuple.Create(UnitType.Fib, "FIB"),
        Tuple.Create(UnitType.FibHrt, "FIB HRT"),
        Tuple.Create(UnitType.Ems, "EMS"),
    };

    private static void RebuildCallMenu(
        UIMenu menu,
        UnitSource source,
        Dictionary<UIMenuCheckboxItem, Unit> checkboxMap,
        Dictionary<UIMenuItem, UnitType> callItemMap)
    {
        menu.Clear();
        checkboxMap.Clear();
        callItemMap.Clear();

        AddSection(menu, "CALL UNIT");

        foreach (var entry in CallableUnitTypes)
        {
            var item = new UIMenuItem(entry.Item2);

            menu.AddItem(item);
            callItemMap[item] = entry.Item1;
        }

        AddSection(menu, "ACTIVE UNITS");

        var units =
            source == UnitSource.Code2
                ? UnitManager.Code2Units
                : UnitManager.Code3Units;

        foreach (var unit in units)
        {
            string idText = unit.Id.ToString();
            string shortId =
                idText.Length > 4
                    ? idText.Substring(0, 4)
                    : idText;

            string label =
                string.Format(
                    "{0} #{1} [{2}]",
                    unit.Type,
                    shortId,
                    unit.Lifecycle);

            var checkbox =
                new UIMenuCheckboxItem(
                    label,
                    unit.IsSelected);

            menu.AddItem(checkbox);
            checkboxMap[checkbox] = unit;
        }

        AddSection(menu, "COMMANDS");

        menu.AddItem(new UIMenuItem("Follow Me"));
        menu.AddItem(new UIMenuItem("Patrol [100m]"));
        menu.AddItem(new UIMenuItem("Regroup"));
        menu.AddItem(new UIMenuItem("Hold Position"));

        AddSection(menu, "EMS");

        menu.AddItem(new UIMenuItem("Treat Ped"));
        menu.AddItem(new UIMenuItem("Treat Officer"));
        menu.AddItem(new UIMenuItem("Treat Suspect"));
        menu.AddItem(new UIMenuItem("Load Patient"));
        menu.AddItem(new UIMenuItem("Unload Patient"));
    }

    private static void OnCallMenuItemSelect(
        UIMenu menu,
        UnitSource source,
        UIMenuItem item,
        Dictionary<UIMenuCheckboxItem, Unit> checkboxMap,
        Dictionary<UIMenuItem, UnitType> callItemMap)
    {
        UnitType type;

        if (callItemMap.TryGetValue(item, out type))
        {
            SpawnDispatcher.Dispatch(type, source);

            RebuildCallMenu(
                menu,
                source,
                checkboxMap,
                callItemMap);

            return;
        }

        var units =
            (source == UnitSource.Code2
                ? UnitManager.Code2Units
                : UnitManager.Code3Units)
            .Where(u => u.IsSelected)
            .ToList();

        switch (item.Text)
        {
            case "Follow Me":
                MovementDispatcher.FollowMe(units);
                break;

            case "Patrol [100m]":
                MovementDispatcher.Patrol(units);
                break;

            case "Regroup":
                MovementDispatcher.Regroup(units);
                break;

            case "Hold Position":
                MovementDispatcher.HoldPosition(units);
                break;

            case "Treat Ped":
            {
                var emsUnit =
                    units.FirstOrDefault(
                        u => u.Type == UnitType.Ems);

                var patient =
                    TargetingHelper.GetAimedOrNearestPed();

                if (emsUnit != null &&
                    patient != null)
                {
                    EmsDispatcher.Treat(
                        emsUnit,
                        patient);
                }

                break;
            }

            case "Treat Officer":
            {
                var emsUnit =
                    units.FirstOrDefault(
                        u => u.Type == UnitType.Ems);

                if (emsUnit == null)
                    break;

                var officer =
                    FindNearestOfficerPed(
                        emsUnit.Ped.Position);

                if (officer != null)
                {
                    EmsDispatcher.Treat(
                        emsUnit,
                        officer);
                }

                break;
            }

            case "Treat Suspect":
            {
                var emsUnit =
                    units.FirstOrDefault(
                        u => u.Type == UnitType.Ems);

                if (emsUnit == null)
                    break;

                var suspect =
                    FindNearestCuffedPed(
                        emsUnit.Ped.Position)
                    ?? TargetingHelper.GetAimedOrNearestPed();

                if (suspect != null)
                {
                    EmsDispatcher.Treat(
                        emsUnit,
                        suspect);
                }

                break;
            }

            case "Load Patient":
            {
                var emsUnit =
                    units.FirstOrDefault(
                        u => u.Type == UnitType.Ems);

                var patient =
                    TargetingHelper.GetAimedOrNearestPed();

                if (emsUnit != null &&
                    patient != null &&
                    emsUnit.Vehicle != null)
                {
                    EmsDispatcher.LoadPatient(
                        emsUnit,
                        patient,
                        emsUnit.Vehicle);
                }

                break;
            }

            case "Unload Patient":
            {
                var patient =
                    TargetingHelper.GetAimedOrNearestPed();

                if (patient != null)
                    EmsDispatcher.UnloadPatient(patient);

                break;
            }
        }
    }

    private static Ped FindNearestOfficerPed(
        Vector3 from,
        float radius = 30f)
    {
        return UnitManager.AllUnits
            .Where(
                u => u.IsAlive &&
                     u.Ped.Position.DistanceTo(from) <= radius)
            .OrderBy(
                u => u.Ped.Position.DistanceTo(from))
            .Select(u => u.Ped)
            .FirstOrDefault();
    }

    private static Ped FindNearestCuffedPed(
        Vector3 from,
        float radius = 30f)
    {
        Ped nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ped in World.GetAllPeds())
        {
            if (ped == null ||
                !ped.Exists())
                continue;

            float dist =
                ped.Position.DistanceTo(from);

            if (dist > radius)
                continue;

            bool isCuffed =
                NativeFunction.Natives
                    .IS_PED_CUFFED<bool>(ped);

            if (!isCuffed)
                continue;

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ped;
            }
        }

        return nearest;
    }
}

}
