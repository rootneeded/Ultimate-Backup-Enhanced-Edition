using System;
using Rage;
using UltimateBackupReplacement.Core;

namespace UltimateBackupReplacement.Units
{
    /// <summary>
    /// Единая модель юнита. Используется и для Partners, и для Code2/Code3 -
    /// разница выражена через UnitSource, а не через отдельные классы,
    /// потому что 90% состояний (State/Status/команды передвижения) у них общие.
    /// </summary>
    public class Unit
    {
        public Guid Id = Guid.NewGuid();

        public UnitSource Source;
        public UnitType Type;
        public UnitStatus Status = UnitStatus.OnFoot;
        public UnitState State = UnitState.Idle;
        public UnitLifecycle Lifecycle = UnitLifecycle.Active;

        public Ped Ped;
        public Vehicle Vehicle; // текущий транспорт (если в нём находится)

        /// <summary>
        /// Только для Partner: персональная "своя" машина, назначается один раз при спавне.
        /// При Get In Vehicle без явного target'а партнёр всегда садится именно сюда, за руль.
        /// </summary>
        public Vehicle AssignedVehicle;

        public bool IsSelected;

        /// <summary>Текущая цель команды (suspect, patient, машина для обыска и т.п.)</summary>
        public Ped CurrentTarget;
        public Vehicle CurrentTargetVehicle;

        /// <summary>Действие, которое нужно выполнить после промежуточного состояния (например после выхода из машины).</summary>
        public Action PendingCommand;

        // --- Watchdog / cover-таймеры ---
        public Vector3 LastKnownPosition;
        public float StuckTimer;
        public float TimeInVehicleCover;
        public int CoverSearchStartTime;
        public bool CurrentCoverIsVehicle;
        public float LastHealth = -1f; // -1 = ещё не инициализировано

        public bool IsPartner => Source == UnitSource.Partner;
        public bool IsAlive => Ped != null && Ped.Exists() && !Ped.IsDead;

        public Unit(Ped ped, UnitSource source, UnitType type)
        {
            Ped = ped;
            Source = source;
            Type = type;
            LastKnownPosition = ped?.Position ?? Vector3.Zero;
        }

        public void SetVehicle(Vehicle v)
        {
            Vehicle = v;
            Status = v != null ? UnitStatus.InVehicle : UnitStatus.OnFoot;
        }
    }
}
