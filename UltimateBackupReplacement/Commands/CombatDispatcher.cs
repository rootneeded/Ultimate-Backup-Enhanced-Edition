using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
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

        /// <summary>
        /// Эти таймеры инкрементятся не через Game.FrameTime (это дельта реального игрового
        /// кадра, ~16мс), а фиксированным шагом - CheckUnderFire/UpdateCoverBehavior/WatchdogTick
        /// вызываются из SlowLoop раз в SlowLoopIntervalSec, а не каждый кадр (см. Main.cs).
        /// Использование Game.FrameTime здесь занижало бы накопление таймеров примерно в 12 раз
        /// и ломало бы все длительности (10 сек релокейта укрытия растянулись бы на ~2 минуты).
        /// </summary>
        private const float SlowLoopIntervalSec = 0.2f;

        private const float UnderFireScanRadius = 50f;

        /// <summary>
        /// Вызывать из главного Tick для КАЖДОГО активного юнита (Partner + Code2 + Code3).
        /// Проверяет, не попадает ли юнит под огонь, и переводит в SeekingCover.
        ///
        /// GET_PED_SOURCE_OF_DEATH реагирует только на смерть, а не на входящий урон -
        /// для живого юнита он бесполезен. Вместо этого отслеживаем падение Health между
        /// проверками (единственный надёжный сигнал "кто-то попал") и, если оно упало,
        /// ищем ближайшего реально стреляющего ped'а в радиусе как источник угрозы.
        /// </summary>
        public static void CheckUnderFire(Unit unit)
        {
            if (unit.State == UnitState.Downed) return;
            if (!unit.Ped.Exists() || unit.Ped.IsDead) return;

            if (unit.LastHealth < 0f)
            {
                unit.LastHealth = unit.Ped.Health;
                return; // первая проверка - только запоминаем baseline, реагировать не на что
            }

            bool tookDamage = unit.Ped.Health < unit.LastHealth - 0.5f; // небольшой допуск на шум
            unit.LastHealth = unit.Ped.Health;

            if (!tookDamage) return;
            if (unit.State == UnitState.SeekingCover || unit.State == UnitState.InCover || unit.State == UnitState.CombatOpen)
                return; // уже реагирует

            Ped attacker = FindNearestShooter(unit.Ped.Position, UnderFireScanRadius);
            if (attacker == null) return; // урон не от огнестрела (падение, взрыв и т.п.) - не переключаем в комбат

            unit.CurrentTarget = attacker;
            unit.State = UnitState.SeekingCover;
            unit.CoverSearchStartTime = Environment.TickCount;

            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(unit.Ped, 0 /* BF_CanUseCover */, true);
            NativeFunction.Natives.TASK_COMBAT_PED(unit.Ped, attacker, 0, 16);
        }

        private static Ped FindNearestShooter(Vector3 position, float radius)
        {
            Ped nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var ped in World.GetAllPeds())
            {
                if (ped == null || !ped.Exists() || ped.IsDead) continue;
                float dist = ped.Position.DistanceTo(position);
                if (dist > radius) continue;

                bool isShooting = NativeFunction.Natives.IS_PED_SHOOTING<bool>(ped);
                if (!isShooting) continue;

                if (dist < nearestDist) { nearestDist = dist; nearest = ped; }
            }

            return nearest;
        }

        /// <summary>Вызывать каждый тик для юнитов в состояниях SeekingCover/InCover.</summary>
        public static void UpdateCoverBehavior(Unit unit)
        {
            if (!unit.Ped.Exists() || unit.Ped.IsDead) return;

            if (unit.State == UnitState.SeekingCover)
            {
                bool inCover = NativeFunction.Natives.IS_PED_IN_COVER<bool>(unit.Ped, false);

                if (inCover)
                {
                    unit.State = UnitState.InCover;
                    unit.TimeInVehicleCover = 0f;
                    RegisterCoverEntityIfVehicle(unit);
                }
                else if (Environment.TickCount - unit.CoverSearchStartTime > CoverSearchTimeoutMs)
                {
                    // укрытие не найдено за разумное время - стреляем в открытую
                    unit.State = UnitState.CombatOpen;
                    NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(unit.Ped, 0, false);
                }
                return;
            }

            if (unit.State == UnitState.InCover)
            {
                // Машина - временное укрытие, может рвануть. Через фиксированное время
                // юнит меняет позицию на более надёжную, независимо от реального состояния тачки.
                if (unit.CurrentCoverIsVehicle)
                {
                    unit.TimeInVehicleCover += SlowLoopIntervalSec;
                    if (unit.TimeInVehicleCover > VehicleCoverRelocateSec)
                    {
                        unit.State = UnitState.SeekingCover;
                        unit.CurrentCoverIsVehicle = false;
                        unit.TimeInVehicleCover = 0f;
                        unit.CoverSearchStartTime = Environment.TickCount;
                        NativeFunction.Natives.TASK_COMBAT_PED(unit.Ped, unit.CurrentTarget, 0, 16);
                    }
                }
            }
        }

        private static void RegisterCoverEntityIfVehicle(Unit unit)
        {
            // Точного нативного способа получить cover-entity под рукой обычно нет в SHVDN/RPH обёртке -
            // приближённая проверка: есть ли машина в радиусе ~2м от текущей cover-позиции пеша.
            var nearbyVehicle = World.GetClosestVehicle(unit.Ped.Position, 2.5f);
            unit.CurrentCoverIsVehicle = nearbyVehicle != null && nearbyVehicle.Exists();
        }

        /// <summary>Generic watchdog: если юнит не двигается и не меняет State слишком долго - сброс в CombatOpen/Idle.</summary>
        public static void WatchdogTick(Unit unit)
        {
            if (!unit.Ped.Exists()) return;

            float moved = unit.Ped.Position.DistanceTo(unit.LastKnownPosition);
            if (moved < StuckMoveThreshold)
            {
                unit.StuckTimer += SlowLoopIntervalSec;
                if (unit.StuckTimer > StuckTimeoutSec &&
                    (unit.State == UnitState.SeekingCover || unit.State == UnitState.RunningToTakedown))
                {
                    unit.State = UnitState.CombatOpen;
                    unit.StuckTimer = 0f;
                }
            }
            else
            {
                unit.StuckTimer = 0f;
                unit.LastKnownPosition = unit.Ped.Position;
            }
        }

        // --- Open Fire (бинд U) ---

        /// <summary>Тап U: стреляет один (ближайший к точке прицела) партнёр.</summary>
        public static void OpenFireSingle(IEnumerable<Unit> candidates, Vector3 aimPoint)
        {
            var unit = candidates
                .Where(u => u.IsAlive)
                .OrderBy(u => u.Ped.Position.DistanceTo(aimPoint))
                .FirstOrDefault();

            if (unit != null) OpenFireOne(unit, aimPoint);
        }

        /// <summary>Холд U: стреляют все выбранные партнёры одновременно, все в одну точку прицела.</summary>
        public static void OpenFireAll(IEnumerable<Unit> candidates, Vector3 aimPoint)
        {
            foreach (var unit in candidates.Where(u => u.IsAlive))
                OpenFireOne(unit, aimPoint);
        }

        private static void OpenFireOne(Unit unit, Vector3 aimPoint)
        {
            unit.State = UnitState.OpenFire;
            int magazineSize = NativeFunction.Natives.GET_MAX_AMMO_IN_CLIP<int>(unit.Ped, unit.Ped.Inventory.EquippedWeapon?.Hash ?? WeaponHash.Pistol, true);

            var possibleTarget = World.GetClosestPed(aimPoint, 2f);

            GameFiber.StartNew(() =>
            {
                int shots = 0;
                unit.Ped.Tasks.AimWeaponAt(aimPoint, -1);

                while (shots < magazineSize && unit.Ped.Exists())
                {
                    if (possibleTarget != null && possibleTarget.Exists() && possibleTarget.IsDead)
                        break; // цель поражена - не досаживаем обойму

                    NativeFunction.Natives.FIRE_SINGLE_SHOT_DIRECTLY_AT_COORD(unit.Ped, aimPoint.X, aimPoint.Y, aimPoint.Z);
                    shots++;
                    GameFiber.Sleep(120); // темп стрельбы, подогнать под rate of fire оружия
                }

                unit.Ped.Tasks.Clear();
                unit.State = UnitState.Idle;
            });
        }
    }
}
