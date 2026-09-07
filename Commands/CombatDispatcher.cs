
using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class CombatDispatcher
    {
        private const float CoverSearchTimeoutMs = 2500f;
        private const float VehicleCoverRelocateSec = 10f;
        private const float StuckTimeoutSec = 4f;
        private const float StuckMoveThreshold = 0.15f;

        private const float SlowLoopIntervalSec = 0.2f;
        private const float UnderFireScanRadius = 50f;

        public static void CheckUnderFire(Unit unit)
        {
            if (unit == null ||
                unit.State == UnitState.Downed ||
                !unit.Ped.Exists() ||
                unit.Ped.IsDead)
                return;

            if (unit.LastHealth < 0f)
            {
                unit.LastHealth = unit.Ped.Health;
                return;
            }

            bool tookDamage =
                unit.Ped.Health < unit.LastHealth - 0.5f;

            unit.LastHealth = unit.Ped.Health;

            if (!tookDamage)
                return;

            if (unit.State == UnitState.SeekingCover ||
                unit.State == UnitState.InCover ||
                unit.State == UnitState.CombatOpen)
                return;

            Ped attacker =
                FindNearestShooter(
                    unit.Ped.Position,
                    UnderFireScanRadius);

            if (attacker == null)
                return;

            unit.CurrentTarget = attacker;
            unit.State = UnitState.SeekingCover;
            unit.CoverSearchStartTime = Environment.TickCount;

            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(
                unit.Ped,
                0,
                true);

            NativeFunction.Natives.TASK_COMBAT_PED(
                unit.Ped,
                attacker,
                0,
                16);
        }

        private static Ped FindNearestShooter(
            Vector3 position,
            float radius)
        {
            Ped nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var ped in World.GetAllPeds())
            {
                if (ped == null ||
                    !ped.Exists() ||
                    ped.IsDead)
                    continue;

                float dist =
                    ped.Position.DistanceTo(position);

                if (dist > radius)
                    continue;

                bool isShooting =
                    NativeFunction.Natives.IS_PED_SHOOTING<bool>(
                        ped);

                if (!isShooting)
                    continue;

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = ped;
                }
            }

            return nearest;
        }

        public static void UpdateCoverBehavior(Unit unit)
        {
            if (unit == null ||
                !unit.Ped.Exists() ||
                unit.Ped.IsDead)
                return;

            if (unit.State == UnitState.SeekingCover)
            {
                bool inCover =
                    NativeFunction.Natives.IS_PED_IN_COVER<bool>(
                        unit.Ped,
                        false);

                if (inCover)
                {
                    unit.State = UnitState.InCover;
                    unit.TimeInVehicleCover = 0f;
                    RegisterCoverEntityIfVehicle(unit);
                }
                else if (Environment.TickCount -
                         unit.CoverSearchStartTime >
                         CoverSearchTimeoutMs)
                {
                    unit.State = UnitState.CombatOpen;

                    NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(
                        unit.Ped,
                        0,
                        false);
                }

                return;
            }

            if (unit.State == UnitState.InCover &&
                unit.CurrentCoverIsVehicle)
            {
                unit.TimeInVehicleCover +=
                    SlowLoopIntervalSec;

                if (unit.TimeInVehicleCover >
                    VehicleCoverRelocateSec)
                {
                    unit.State = UnitState.SeekingCover;
                    unit.CurrentCoverIsVehicle = false;
                    unit.TimeInVehicleCover = 0f;
                    unit.CoverSearchStartTime =
                        Environment.TickCount;

                    if (unit.CurrentTarget != null &&
                        unit.CurrentTarget.Exists())
                    {
                        NativeFunction.Natives.TASK_COMBAT_PED(
                            unit.Ped,
                            unit.CurrentTarget,
                            0,
                            16);
                    }
                }
            }
        }

        private static void RegisterCoverEntityIfVehicle(Unit unit)
        {
            var entity =
                World.GetClosestEntity(
                    unit.Ped.Position,
                    2.5f,
                    GetEntitiesFlags.Vehicles);

            unit.CurrentCoverIsVehicle =
                entity != null && entity.Exists();
        }

        public static void WatchdogTick(Unit unit)
        {
            if (unit == null ||
                !unit.Ped.Exists())
                return;

            float moved =
                unit.Ped.Position.DistanceTo(
                    unit.LastKnownPosition);

            if (moved < StuckMoveThreshold)
            {
                unit.StuckTimer +=
                    SlowLoopIntervalSec;

                if (unit.StuckTimer > StuckTimeoutSec &&
                    (unit.State == UnitState.SeekingCover ||
                     unit.State == UnitState.RunningToTakedown))
                {
                    unit.State = UnitState.CombatOpen;
                    unit.StuckTimer = 0f;
                    unit.LastKnownPosition =
                        unit.Ped.Position;
                }
            }
            else
            {
                unit.StuckTimer = 0f;
                unit.LastKnownPosition =
                    unit.Ped.Position;
            }
        }

        public static void OpenFireSingle(
            IEnumerable<Unit> candidates,
            Vector3 aimPoint)
        {
            var unit = candidates
                .Where(u => u != null && u.IsAlive)
                .OrderBy(u =>
                    u.Ped.Position.DistanceTo(aimPoint))
                .FirstOrDefault();

            if (unit != null)
                OpenFireOne(unit, aimPoint);
        }

        public static void OpenFireAll(
            IEnumerable<Unit> candidates,
            Vector3 aimPoint)
        {
            foreach (var unit in candidates.Where(
                u => u != null && u.IsAlive))
            {
                OpenFireOne(unit, aimPoint);
            }
        }

        private static void OpenFireOne(
            Unit unit,
            Vector3 aimPoint)
        {
            unit.State = UnitState.OpenFire;

            WeaponHash weaponHash =
                WeaponHash.Pistol;

            var equippedWeapon =
                unit.Ped.Inventory.EquippedWeapon;

            if (equippedWeapon != null)
                weaponHash = equippedWeapon.Hash;

            int magazineSize =
                NativeFunction.Natives.GET_MAX_AMMO_IN_CLIP<int>(
                    unit.Ped,
                    weaponHash,
                    true);

            if (magazineSize <= 0)
                magazineSize = 12;

            GameFiber.StartNew(() =>
            {
                int shots = 0;

                if (!unit.Ped.Exists())
                    return;

                unit.Ped.Tasks.AimWeaponAt(
                    aimPoint,
                    -1);

                while (shots < magazineSize &&
                       unit.Ped.Exists())
                {
                    NativeFunction.Natives.FIRE_SINGLE_SHOT_DIRECTLY_AT_COORD(
                        unit.Ped,
                        aimPoint.X,
                        aimPoint.Y,
                        aimPoint.Z);

                    shots++;

                    GameFiber.Sleep(120);
                }

                if (unit.Ped.Exists())
                    unit.Ped.Tasks.Clear();

                unit.State = UnitState.Idle;
            });
        }
    }
}

