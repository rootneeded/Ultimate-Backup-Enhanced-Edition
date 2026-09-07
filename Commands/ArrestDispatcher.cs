
using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Compatibility;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class ArrestDispatcher
    {
        private static readonly Random Rnd = new Random();

        private const float WarningIntervalMs = 1500f;
        private const float CoveringRadius = 4f;
        private const float TaseRange = 12f;

        private const string WarningVoiceContext1 =
            "ARREST_GENERIC_WARNING_01";

        private const string WarningVoiceContext2 =
            "ARREST_GENERIC_WARNING_02";

        public static void Arrest(Unit unit, Ped target)
        {
            if (unit == null ||
                !TargetingHelper.IsValidSuspectTarget(target))
                return;

            if (unit.Status == UnitStatus.InVehicle)
            {
                unit.State = UnitState.ExitingVehicleForCommand;
                unit.PendingCommand = () =>
                    Arrest(unit, target);

                MovementDispatcher.ExitVehicle(unit);
                return;
            }

            unit.CurrentTarget = target;

            unit.Ped.Tasks.FollowNavigationMeshToPosition(
                target.Position,
                target.Heading,
                1f);

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() &&
                       target.Exists() &&
                       unit.Ped.Position.DistanceTo(target.Position) > 1.5f)
                {
                    GameFiber.Sleep(100);
                }

                if (!target.Exists() ||
                    !unit.Ped.Exists())
                    return;

                bool handled =
                    LspdfrBridge.IsLoaded() &&
                    LspdfrBridge.Arrest(unit.Ped, target);

                if (!handled)
                {
                    NativeFunction.Natives.TASK_ARREST_PED(
                        unit.Ped,
                        target);
                }

                unit.State = UnitState.Idle;
            });
        }

        public static void ForceArrest(
            List<Unit> selectedUnits,
            Ped target)
        {
            if (selectedUnits == null ||
                selectedUnits.Count == 0 ||
                !TargetingHelper.IsValidSuspectTarget(target))
                return;

            var validUnits = selectedUnits
                .Where(u => u != null && u.IsAlive)
                .ToList();

            if (validUnits.Count == 0)
                return;

            var primary =
                validUnits[Rnd.Next(validUnits.Count)];

            var covering =
                validUnits
                    .Where(u => u != primary)
                    .ToList();

            TriggerFleeIfSpotted(
                target,
                primary);

            RunPrimary(primary, target);

            int index = 0;

            foreach (var unit in covering)
            {
                RunCovering(
                    unit,
                    primary,
                    target,
                    index,
                    covering.Count);

                index++;
            }
        }

        private static void TriggerFleeIfSpotted(
            Ped target,
            Unit closestUnit)
        {
            if (target == null ||
                closestUnit == null ||
                !target.Exists() ||
                !closestUnit.Ped.Exists())
                return;

            float distance =
                target.Position.DistanceTo(
                    closestUnit.Ped.Position);

            if (distance > 25f)
                return;

            bool hasLos =
                !World.TraceLine(
                    target.Position + new Vector3(0f, 0f, 0.6f),
                    closestUnit.Ped.Position +
                        new Vector3(0f, 0f, 0.6f),
                    TraceFlags.IntersectWorld).Hit;

            if (!hasLos)
                return;

            target.Tasks.ReactAndFlee(
                closestUnit.Ped);
        }

        private static void RunPrimary(
            Unit primary,
            Ped target)
        {
            if (primary == null ||
                target == null ||
                !target.Exists())
                return;

            if (primary.Status == UnitStatus.InVehicle)
            {
                primary.State =
                    UnitState.ExitingVehicleForCommand;

                primary.PendingCommand = () =>
                    RunPrimary(primary, target);

                MovementDispatcher.ExitVehicle(primary);
                return;
            }

            primary.CurrentTarget = target;
            primary.State = UnitState.RunningToTakedown;

            primary.Ped.Tasks.FollowNavigationMeshToPosition(
                target.Position,
                target.Heading,
                1.8f);

            GameFiber.StartNew(() =>
            {
                const int pollMs = 300;
                const int maxWaitMs = 3000;

                int waited = 0;
                bool fled = false;

                while (primary.Ped.Exists() &&
                       target.Exists() &&
                       primary.Ped.Position.DistanceTo(
                           target.Position) > 1.8f &&
                       waited < maxWaitMs)
                {
                    if (IsFleeing(target))
                    {
                        fled = true;
                        break;
                    }

                    GameFiber.Sleep(pollMs);
                    waited += pollMs;
                }

                if (!primary.Ped.Exists() ||
                    !target.Exists())
                {
                    primary.State = UnitState.Idle;
                    return;
                }

                if (fled || IsFleeing(target))
                {
                    RunWarningsThenSubdue(
                        primary,
                        target);
                }
                else
                {
                    DoPhysicalTakedown(
                        primary,
                        target);
                }
            });
        }

        private static void RunWarningsThenSubdue(
            Unit primary,
            Ped target)
        {
            PlayWarningVoiceLine(primary, 1);

            GameFiber.Sleep(
                (int)WarningIntervalMs);

            if (IsCompliant(target))
            {
                DoPhysicalTakedown(primary, target);
                return;
            }

            PlayWarningVoiceLine(primary, 2);

            GameFiber.Sleep(
                (int)WarningIntervalMs);

            if (IsCompliant(target))
            {
                DoPhysicalTakedown(primary, target);
                return;
            }

            bool hasTaser =
                primary.Ped.Inventory.Weapons.Any(
                    w => w.Hash == WeaponHash.StunGun);

            float distance =
                primary.Ped.Position.DistanceTo(
                    target.Position);

            if (hasTaser &&
                distance <= TaseRange)
            {
                UseTaser(primary, target);
            }
            else
            {
                DoPhysicalTakedown(primary, target);
            }
        }

        private static bool IsFleeing(Ped target)
        {
            return target != null &&
                   target.Exists() &&
                   (target.IsRunning ||
                    target.IsSprinting);
        }

        private static bool IsCompliant(Ped target)
        {
            return target == null ||
                   !target.Exists() ||
                   target.IsDead ||
                   (!target.IsRunning &&
                    !target.IsSprinting);
        }

        private static void PlayWarningVoiceLine(
            Unit unit,
            int stage)
        {
            string context =
                stage == 1
                    ? WarningVoiceContext1
                    : WarningVoiceContext2;

            NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(
                unit.Ped,
                context,
                "SPEECH_PARAMS_FORCE_SHOUTED");
        }

        private static void UseTaser(
            Unit unit,
            Ped target)
        {
            unit.State = UnitState.TaseringSuspect;

            var stunGun =
                unit.Ped.Inventory.Weapons.FirstOrDefault(
                    w => w.Hash == WeaponHash.StunGun);

            if (stunGun == null)
            {
                DoPhysicalTakedown(unit, target);
                return;
            }

            unit.Ped.Inventory.EquippedWeapon =
                stunGun;

            unit.Ped.Tasks
                .AimWeaponAt(target, 1500)
                .WaitForCompletion(2000);

            unit.Ped.Tasks
                .FireWeaponAt(
                    target,
                    1000,
                    FiringPattern.SingleShot)
                .WaitForCompletion(1500);

            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(1500);

                if (unit.Ped.Exists() &&
                    target.Exists() &&
                    !target.IsDead)
                {
                    NativeFunction.Natives.TASK_ARREST_PED(
                        unit.Ped,
                        target);
                }

                unit.State = UnitState.Idle;
            });
        }

        private static void DoPhysicalTakedown(
            Unit unit,
            Ped target)
        {
            if (!unit.Ped.Exists() ||
                !target.Exists())
                return;

            unit.State =
                UnitState.RunningToTakedown;

            unit.Ped.Tasks.FollowNavigationMeshToPosition(
                target.Position,
                target.Heading,
                1.2f);

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() &&
                       target.Exists() &&
                       unit.Ped.Position.DistanceTo(
                           target.Position) > 1.8f)
                {
                    GameFiber.Sleep(100);
                }

                if (!unit.Ped.Exists() ||
                    !target.Exists())
                {
                    unit.State = UnitState.Idle;
                    return;
                }

                NativeFunction.Natives.TASK_PLAY_ANIM(
                    unit.Ped,
                    "mp_arresting",
                    "idle",
                    8f,
                    -8f,
                    1000,
                    0,
                    0,
                    false,
                    false,
                    false);

                GameFiber.Sleep(1000);

                if (unit.Ped.Exists() &&
                    target.Exists())
                {
                    NativeFunction.Natives.TASK_ARREST_PED(
                        unit.Ped,
                        target);
                }

                unit.State = UnitState.Idle;
            });
        }

        private static void RunCovering(
            Unit unit,
            Unit primary,
            Ped target,
            int index,
            int totalCovering)
        {
            if (unit == null ||
                primary == null ||
                target == null ||
                !target.Exists())
                return;

            if (unit.Status == UnitStatus.InVehicle)
            {
                unit.State =
                    UnitState.ExitingVehicleForCommand;

                unit.PendingCommand = () =>
                    RunCovering(
                        unit,
                        primary,
                        target,
                        index,
                        totalCovering);

                MovementDispatcher.ExitVehicle(unit);
                return;
            }

            unit.CurrentTarget = target;
            unit.State = UnitState.CoveringAimed;

            Vector3 pos =
                GetCoveringPosition(
                    target,
                    index,
                    totalCovering,
                    CoveringRadius);

            unit.Ped.Tasks.FollowNavigationMeshToPosition(
                pos,
                target.Heading,
                1f);

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() &&
                       unit.Ped.Position.DistanceTo(pos) > 1.5f &&
                       unit.State == UnitState.CoveringAimed &&
                       primary.State != UnitState.Idle)
                {
                    GameFiber.Sleep(100);
                }

                if (unit.Ped.Exists() &&
                    unit.State == UnitState.CoveringAimed &&
                    target.Exists() &&
                    primary.State != UnitState.Idle)
                {
                    unit.Ped.Tasks.AimWeaponAt(
                        target,
                        -1);
                }

                while (unit.Ped.Exists() &&
                       unit.State == UnitState.CoveringAimed &&
                       primary.State != UnitState.Idle)
                {
                    GameFiber.Sleep(200);
                }

                if (unit.Ped.Exists() &&
                    unit.State == UnitState.CoveringAimed)
                {
                    unit.Ped.Tasks.Clear();
                    unit.State = UnitState.Idle;
                }
            });
        }

        private static Vector3 GetCoveringPosition(
            Ped target,
            int index,
            int total,
            float radius)
        {
            float angleStep =
                360f / Math.Max(total, 1);

            float angleDeg =
                angleStep * index;

            float rad =
                angleDeg *
                (float)(Math.PI / 180.0);

            var offset =
                new Vector3(
                    (float)Math.Cos(rad) * radius,
                    (float)Math.Sin(rad) * radius,
                    0f);

            return target.Position + offset;
        }
    }
}
