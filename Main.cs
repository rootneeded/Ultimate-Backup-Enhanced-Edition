using Rage;
using UltimateBackupReplacement.Commands;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Input;
using UltimateBackupReplacement.UI;
using UltimateBackupReplacement.Units;

[assembly: Rage.Attributes.Plugin("Ultimate Backup Replacement", Author = "You", PrefersSingleInstance = true)]

namespace UltimateBackupReplacement
{
    public class Main
    {
        /// <summary>
        /// Разведены два независимых цикла:
        ///  - FastLoop (каждый кадр, ~60/сек) - только ввод. Нужен per-frame для edge-detection
        ///    (B/O/T не должны спамить, пока клавиша зажата) и для точного тайминга hold vs tap на U.
        ///  - SlowLoop (раз в ~200мс) - вся тяжёлая AI-логика по юнитам (auto-cover, watchdog,
        ///    "вышел из машины -> выполнить отложенную команду", покойники из списка). Эта логика
        ///    не нуждается в частоте 60 раз в секунду - реакция "укрылся за 200мс вместо 16мс"
        ///    визуально неотличима, а нагрузка на CPU при большом числе юнитов (8+ NOOSE, 8+ SWAT,
        ///    несколько партнёров) падает в разы по сравнению с прогоном всего этого каждый кадр.
        /// </summary>
        public static void Main_()
        {
            GameFiber.StartNew(FastLoop, "UBR Fast Loop (input)");
            GameFiber.StartNew(SlowLoop, "UBR Slow Loop (unit AI)");
        }

        private static void FastLoop()
        {
            while (true)
            {
                GameFiber.Yield();
                NativeMenu.Tick();
                KeyBindings.Tick();
            }
        }

        private static void SlowLoop()
        {
            const int intervalMs = 200;

            while (true)
            {
                GameFiber.Sleep(intervalMs);

                foreach (var unit in UnitManager.AllUnits)
                    TickUnit(unit);

                UnitManager.PurgeDead();
            }
        }

        private static void TickUnit(Unit unit)
        {
            if (!unit.IsAlive) return;

            // Промежуточное состояние "вышел из машины перед выполнением команды"
            if (unit.State == UnitState.ExitingVehicleForCommand)
            {
                if (unit.Status == UnitStatus.OnFoot)
                {
                    var pending = unit.PendingCommand;
                    unit.PendingCommand = null;
                    pending?.Invoke();
                }
                return;
            }

            // Боевая логика - для ВСЕХ юнитов (Partners + Code2 + Code3)
            CombatDispatcher.CheckUnderFire(unit);
            CombatDispatcher.UpdateCoverBehavior(unit);
            CombatDispatcher.WatchdogTick(unit);
        }
    }
}
