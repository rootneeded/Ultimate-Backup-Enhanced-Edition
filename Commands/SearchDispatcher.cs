
using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class SearchDispatcher
    {
        private const float SearchDurationMs = 4000f;
        private const float GuardRadius = 5f;

        public static void SearchPed(List<Unit> selectedUnits, Ped target)
        {
            if (selectedUnits == null ||
                selectedUnits.Count == 0 ||
                !TargetingHelper.IsValidSuspectTarget(target))
                return;

            var primary = selectedUnits
                .OrderBy(u => u.Ped.Position.DistanceTo(target.Position))
                .First();

            var guards = selectedUnits
                .Where(u => u != primary)
                .ToList();

            RunSearchPrimary(primary, target.Position, () =>
            {
                NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(
                    primary.Ped,
                    "GENERIC_HI",
                    "SPEECH_PARAMS_STANDARD");
            });

            RunGuards(guards, primary, target.Position);
        }

        public static void SearchVehicle(List<Unit> selectedUnits, Vehicle target)
        {
            if (selectedUnits == null ||
                selectedUnits.Count == 0 ||
                target == null ||
                !target.Exists())
                return;

            var primary = selectedUnits
                .OrderBy(u => u.Ped.Position.DistanceTo(target.Position))
                .First();

            var guards = selectedUnits
                .Where(u => u != primary)
                .ToList();

            RunSearchPrimary(primary, target.Position, () => { });
            RunGuards(guards, primary, target.Position);
        }

        private static void RunSearchPrimary(
            Unit primary,
            Vector3 targetPos,
            Action onFinished)
        {
            if (primary.Status == UnitStatus.InVehicle)
            {
                primary.State = UnitState.ExitingVehicleForCommand;
                primary.PendingCommand = () =>
                    RunSearchPrimary(primary, targetPos, onFinished);

                MovementDispatcher.ExitVehicle(primary);
                return;
            }

            primary.State = UnitState.Searching;

            primary.Ped.Tasks.FollowNavigationMeshToPosition(
                targetPos,
                primary.Ped.Heading,
                1.2f);

            GameFiber.StartNew(() =>
            {
                while (primary.Ped.Exists() &&
                       primary.Ped.Position.DistanceTo(targetPos) > 1.8f &&
                       primary.State == UnitState.Searching)
                {
                    GameFiber.Sleep(100);
                }

                if (!primary.Ped.Exists() ||
                    primary.State != UnitState.Searching)
                    return;

                NativeFunction.Natives.TASK_PLAY_ANIM(
                    primary.Ped,
                    "amb@world_human_stand_impatient@male@no_sign@base",
                    "base",
                    8f,
                    -8f,
                    (int)SearchDurationMs,
                    0,
                    0,
                    false,
                    false,
                    false);

                GameFiber.Sleep((int)SearchDurationMs);

                if (primary.Ped.Exists())
                    onFinished?.Invoke();

                if (primary.State == UnitState.Searching)
                    primary.State = UnitState.Idle;
            });
        }

        private static void RunGuards(
            List<Unit> guards,
            Unit primary,
            Vector3 targetPos)
        {
            int index = 0;

            foreach (var guard in guards)
            {
                int localIndex = index;

                if (guard.Status == UnitStatus.InVehicle)
                {
                    guard.State = UnitState.ExitingVehicleForCommand;

                    guard.PendingCommand = () =>
                        RunSingleGuard(
                            guard,
                            primary,
                            targetPos,
                            localIndex,
                            guards.Count);

                    MovementDispatcher.ExitVehicle(guard);
                }
                else
                {
                    RunSingleGuard(
                        guard,
                        primary,
                        targetPos,
                        localIndex,
                        guards.Count);
                }

                index++;
            }
        }

        private static void RunSingleGuard(
            Unit unit,
            Unit primary,
            Vector3 targetPos,
            int index,
            int total)
        {
            unit.State = UnitState.CoveringAimed;

            float angleStep = 360f / Math.Max(total, 1);
            float rad =
                (angleStep * index) *
                (float)(Math.PI / 180.0);

            var pos = targetPos +
                      new Vector3(
                          (float)Math.Cos(rad) * GuardRadius,
                          (float)Math.Sin(rad) * GuardRadius,
                          0f);

            unit.Ped.Tasks.FollowNavigationMeshToPosition(
                pos,
                0f,
                1f);

            GameFiber.StartNew(() =>
            {
                var rnd = new Random(unit.Id.GetHashCode());

                while (unit.Ped.Exists() &&
                       unit.State == UnitState.CoveringAimed &&
                       (primary.State == UnitState.Searching ||
                        primary.State == UnitState.ExitingVehicleForCommand))
                {
                    float lookAngle =
                        (float)(rnd.NextDouble() * 360.0);

                    NativeFunction.Natives.TASK_ACHIEVE_HEADING(
                        unit.Ped,
                        lookAngle,
                        2000);

                    GameFiber.Sleep(
                        3000 + rnd.Next(2000));
                }

                if (unit.Ped.Exists() &&
                    unit.State == UnitState.CoveringAimed)
                {
                    unit.Ped.Tasks.Clear();
                    unit.State = UnitState.Idle;
                }
            });
        }
    }
}

