using System.Collections.Generic;
using System.Linq;
using UltimateBackupReplacement.Core;

namespace UltimateBackupReplacement.Units
{
    /// <summary>
    /// Единая точка хранения всех юнитов. Меню планшета, бинды (T/O/U) и Dispatcher
    /// работают только через этот класс - не хранят свои собственные списки.
    /// </summary>
    public static class UnitManager
    {
        public static readonly List<Unit> AllUnits = new List<Unit>();

        public static IEnumerable<Unit> Partners => AllUnits.Where(u => u.Source == UnitSource.Partner);
        public static IEnumerable<Unit> Code2Units => AllUnits.Where(u => u.Source == UnitSource.Code2);
        public static IEnumerable<Unit> Code3Units => AllUnits.Where(u => u.Source == UnitSource.Code3);

        public static IEnumerable<Unit> Selected(UnitSource source) =>
            AllUnits.Where(u => u.Source == source && u.IsSelected);

        public static void Register(Unit unit) => AllUnits.Add(unit);

        public static void Remove(Unit unit) => AllUnits.Remove(unit);

        /// <summary>Убирает мёртвых/уничтоженных юнитов из списка. Вызывать раз в некоторое время из основного Tick.</summary>
        public static void PurgeDead()
        {
            AllUnits.RemoveAll(u => !u.IsAlive);
        }

        public static void SelectAll(UnitSource source)
        {
            foreach (var u in AllUnits.Where(u => u.Source == source))
                u.IsSelected = true;
        }

        public static void DeselectAll(UnitSource source)
        {
            foreach (var u in AllUnits.Where(u => u.Source == source))
                u.IsSelected = false;
        }
    }
}
