using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
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

        /// <summary>
        /// ЧЕСТНО: эти speech-контексты придуманы по аналогии с общей схемой именования
        /// GTA V (ambient_speech.rel), а не взяты из реального дампа файлов игры - у меня нет
        /// доступа к твоей установке, чтобы проверить их напрямую. Открой OpenIV -> audio ->
        /// speech, найди voice set своего ped'а (обычно ambient/cop-наборы) и посмотри реальные
        /// имена контекстов (что-то в духе "GENERIC_HI"/"BLOCKED"/"CHAT_STATE" и т.п. - конкретно
        /// под "остановись, полиция" нужно смотреть контексты категории ARREST/COMBAT). Если
        /// строка не существует в voice set пеша - PLAY_PED_AMBIENT_SPEECH_NATIVE просто молча
        /// ничего не скажет, игра не упадёт, но и предупреждения не будет слышно.
        /// </summary>
        private const string WarningVoiceContext1 = "ARREST_GENERIC_WARNING_01";
        private const string WarningVoiceContext2 = "ARREST_GENERIC_WARNING_02";

        /// <summary>Обычный мирный арест - без ролей, без погони, просто cuff.
        /// Пробует LSPDFR Functions.Arrest (правильно триггерит его систему обработки ареста,
        /// статистику и т.п.), при отсутствии LSPDFR - фоллбек на голый нативный TASK_ARREST_PED.</summary>
        public static void Arrest(Unit unit, Ped target)
        {
            if (!TargetingHelper.IsValidSuspectTarget(target)) return;

            if (unit.Status == UnitStatus.InVehicle)
            {
                unit.State = UnitState.ExitingVehicleForCommand;
                unit.PendingCommand = () => Arrest(unit, target);
                MovementDispatcher.ExitVehicle(unit);
                return;
            }

            unit.CurrentTarget = target;
            unit.Ped.Tasks.FollowNavigationMeshToPosition(target.Position, target.Heading, 1f);
            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() && unit.Ped.Position.DistanceTo(target.Position) > 1.5f && target.Exists())
                    GameFiber.Sleep(100);

                if (!target.Exists() || !unit.Ped.Exists()) return;

                bool handled = LspdfrBridge.IsLoaded() && LspdfrBridge.Arrest(unit.Ped, target);
                if (!handled)
                    NativeFunction.Natives.TASK_ARREST_PED(unit.Ped, target);

                unit.State = UnitState.Idle;
            });
        }

        /// <summary>
        /// Force Arrest. selectedUnits - выбранные партнёры. Один случайный становится primary
        /// (бежит на задержание/тайзер), остальные - covering (окружают target на дистанции ~4м,
        /// держат на прицеле). Если suspect убегает - primary даёт 2 голосовых предупреждения,
        /// затем молча применяет тайзер (если есть в экипировке) либо переходит к физическому takedown.
        /// </summary>
        public static void ForceArrest(List<Unit> selectedUnits, Ped target)
        {
            if (selectedUnits == null || selectedUnits.Count == 0 || !TargetingHelper.IsValidSuspectTarget(target))
                return;

            var primary = selectedUnits[Rnd.Next(selectedUnits.Count)];
            var covering = selectedUnits.Where(u => u != primary).ToList();

            // suspect замечает полицию и пытается сбежать (не всегда - см. TriggerFleeIfSpotted)
            TriggerFleeIfSpotted(target, primary);

            RunPrimary(primary, target);

            int index = 0;
            foreach (var u in covering)
            {
                RunCovering(u, primary, target, index, covering.Count);
                index++;
            }
        }

        private static void TriggerFleeIfSpotted(Ped target, Unit closestUnit)
        {
            if (target.HasLineOfSight(closestUnit.Ped) &&
                target.Position.DistanceTo(closestUnit.Ped.Position) < 25f)
            {
                target.Tasks.FleeFrom(closestUnit.Ped, -1);
            }
        }

        /// <summary>
        /// Primary бежит к цели. Побег - не мгновенное решение по одному замеру: пока идёт сближение,
        /// каждые 300мс перепроверяем, не сорвался ли suspect в бег (flee-таск мог не успеть
        /// проявиться в первую же миллисекунду после TriggerFleeIfSpotted). Если добежали до цели
        /// раньше, чем она побежала - обычный takedown. Если она побежала раньше - предупреждения.
        /// </summary>
        private static void RunPrimary(Unit primary, Ped target)
        {
            if (primary.Status == UnitStatus.InVehicle)
            {
                primary.State = UnitState.ExitingVehicleForCommand;
                primary.PendingCommand = () => RunPrimary(primary, target);
                MovementDispatcher.ExitVehicle(primary);
                return;
            }

            primary.CurrentTarget = target;
            primary.State = UnitState.RunningToTakedown;
            primary.Ped.Tasks.FollowNavigationMeshToPosition(target.Position, target.Heading, 1.8f);

            GameFiber.StartNew(() =>
            {
                const int pollMs = 300;
                const int maxWaitMs = 3000; // сколько ждём решения "побежал/не побежал", прежде чем считать "не побежал"
                int waited = 0;
                bool fled = false;

                while (primary.Ped.Exists() && target.Exists() &&
                       primary.Ped.Position.DistanceTo(target.Position) > 1.8f && waited < maxWaitMs)
                {
                    if (IsFleeing(target)) { fled = true; break; }
                    GameFiber.Sleep(pollMs);
                    waited += pollMs;
                }

                if (!primary.Ped.Exists() || !target.Exists()) { primary.State = UnitState.Idle; return; }

                if (fled || IsFleeing(target))
                {
                    RunWarningsThenSubdue(primary, target);
                }
                else
                {
                    DoPhysicalTakedown(primary, target);
                }
            });
        }

        private static void RunWarningsThenSubdue(Unit primary, Ped target)
        {
            PlayWarningVoiceLine(primary, 1);
            GameFiber.Sleep((int)WarningIntervalMs);

            if (IsCompliant(target)) { DoPhysicalTakedown(primary, target); return; }

            PlayWarningVoiceLine(primary, 2);
            GameFiber.Sleep((int)WarningIntervalMs);

            if (IsCompliant(target)) { DoPhysicalTakedown(primary, target); return; }

            bool hasTaser = primary.Ped.Inventory.Weapons.Any(w => w.Hash == WeaponHash.StunGun);
            float distance = primary.Ped.Position.DistanceTo(target.Position);

            if (hasTaser && distance <= TaseRange)
                UseTaser(primary, target);
            else
                DoPhysicalTakedown(primary, target);
        }

        private static bool IsFleeing(Ped target) =>
            target.Exists() && (target.IsRunning || target.IsSprinting);

        private static bool IsCompliant(Ped target) =>
            !target.Exists() || target.IsDead || (!target.IsRunning && !target.IsSprinting);

        private static void PlayWarningVoiceLine(Unit unit, int stage)
        {
            string context = stage == 1 ? WarningVoiceContext1 : WarningVoiceContext2;
            NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(unit.Ped, context, "SPEECH_PARAMS_FORCE_SHOUTED");
        }

        private static void UseTaser(Unit unit, Ped target)
        {
            unit.State = UnitState.TaseringSuspect;
            unit.Ped.Inventory.EquippedWeapon = unit.Ped.Inventory.Weapons.FirstOrDefault(w => w.Hash == WeaponHash.StunGun);
            unit.Ped.Tasks.AimWeaponAt(target, 1500).WaitForCompletion(2000);
            unit.Ped.Tasks.FireWeaponAt(target, 1000, FiringPattern.SingleShot).WaitForCompletion(1500);

            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(300);
                if (target.Exists() && !target.IsDead)
                {
                    // тайзер валит с ног - ragdoll, дальше cuff
                    GameFiber.Sleep(1200);
                    NativeFunction.Natives.TASK_ARREST_PED(unit.Ped, target);
                }
                unit.State = UnitState.Idle;
            });
        }

        private static void DoPhysicalTakedown(Unit unit, Ped target)
        {
            unit.State = UnitState.RunningToTakedown;
            unit.Ped.Tasks.RunTo(target.Position, true, 3000);

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() && target.Exists() &&
                       unit.Ped.Position.DistanceTo(target.Position) > 1.8f)
                    GameFiber.Sleep(100);

                if (!unit.Ped.Exists() || !target.Exists()) { unit.State = UnitState.Idle; return; }

                // Анимация сбивания с ног - заменить на конкретный клип takedown-анимации.
                NativeFunction.Natives.TASK_PLAY_ANIM(unit.Ped, "mp_arresting", "idle", 8f, -8f, -1, 0, 0, false, false, false);
                GameFiber.Sleep(1000);

                NativeFunction.Natives.TASK_ARREST_PED(unit.Ped, target);
                unit.State = UnitState.Idle;
            });
        }

        /// <summary>Covering-юнит подходит на дистанцию CoveringRadius и держит target на прицеле,
        /// пока primary не закончит (State вернётся в Idle) - после этого сам тоже возвращается в Idle,
        /// а не остаётся навечно целиться в уже скрученного suspect'а.</summary>
        private static void RunCovering(Unit unit, Unit primary, Ped target, int index, int totalCovering)
        {
            if (unit.Status == UnitStatus.InVehicle)
            {
                unit.State = UnitState.ExitingVehicleForCommand;
                unit.PendingCommand = () => RunCovering(unit, primary, target, index, totalCovering);
                MovementDispatcher.ExitVehicle(unit);
                return;
            }

            unit.CurrentTarget = target;
            unit.State = UnitState.CoveringAimed;

            Vector3 pos = GetCoveringPosition(target, index, totalCovering, CoveringRadius);
            unit.Ped.Tasks.FollowNavigationMeshToPosition(pos, target.Heading, 1f);

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() && unit.Ped.Position.DistanceTo(pos) > 1.5f &&
                       unit.State == UnitState.CoveringAimed && primary.State != UnitState.Idle)
                    GameFiber.Sleep(100);

                if (unit.Ped.Exists() && unit.State == UnitState.CoveringAimed && target.Exists() &&
                    primary.State != UnitState.Idle)
                {
                    unit.Ped.Tasks.AimWeaponAt(target, -1);
                }

                // Ждём, пока primary завершит арест (его State вернётся в Idle), затем снимаем прицел.
                while (unit.Ped.Exists() && unit.State == UnitState.CoveringAimed && primary.State != UnitState.Idle)
                    GameFiber.Sleep(200);

                if (unit.Ped.Exists() && unit.State == UnitState.CoveringAimed)
                {
                    unit.Ped.Tasks.Clear();
                    unit.State = UnitState.Idle;
                }
            });
        }

        private static Vector3 GetCoveringPosition(Ped target, int index, int total, float radius)
        {
            float angleStep = 360f / Math.Max(total, 1);
            float angleDeg = angleStep * index;
            float rad = angleDeg * (float)(Math.PI / 180.0);
            var offset = new Vector3((float)Math.Cos(rad) * radius, (float)Math.Sin(rad) * radius, 0f);
            return target.Position + offset;
        }
    }
}
