using System;

namespace UltimateBackupReplacement.Core
{
    /// <summary>
    /// Откуда взялся юнит. Определяет, какие бинды/меню на него действуют
    /// и какой у него жизненный цикл (Partner спавнится сразу Active,
    /// Code2/Code3 едут через мир и проходят Dispatched->EnRoute->Arrived->Active).
    /// </summary>
    public enum UnitSource
    {
        Partner,
        Code2,
        Code3
    }

    /// <summary>
    /// Тип подразделения. Определяет набор доступных команд в меню и внешний вид/экипировку.
    /// </summary>
    public enum UnitType
    {
        Police,       // обычный напарник-полицейский (Partners)
        LspdPatrol,
        LssdPatrol,
        StatePatrol,
        Swat,
        Noose,
        NooseSwat,
        Fib,
        FibHrt,
        Ems
    }

    /// <summary>
    /// Пеший юнит или за рулём. Статус динамический, меняется при посадке/высадке.
    /// </summary>
    public enum UnitStatus
    {
        OnFoot,
        InVehicle
    }

    /// <summary>
    /// Жизненный цикл юнита. Актуален только для Code2/Code3 (физически едут через мир).
    /// Partner создаётся сразу в Active.
    /// </summary>
    public enum UnitLifecycle
    {
        Dispatched,   // вызов отдан, юнит ещё физически не заспавнен
        EnRoute,      // заспавнен за пределами видимости, едет к destination
        Arrived,      // доехал, паркуется
        Unloading,    // высадка пассажиров (для NOOSE/SWAT фургонов и т.п.)
        Active         // полностью управляем через меню/AI
    }

    /// <summary>
    /// Текущее поведенческое состояние юнита. Диспетчер меняет State,
    /// Unit.Tick() смотрит на State и гоняет соответствующий Task один раз при смене.
    /// </summary>
    public enum UnitState
    {
        Idle,
        Following,
        Patrolling,
        Convoy,
        Guarding,             // Guard Suspect - стоит рядом, следит чтобы не сбежал
        CoveringAimed,        // держит на прицеле с дистанции (Force Arrest covering)
        Searching,            // Search Ped / Search Vehicle (primary роль)
        RunningToTakedown,    // primary бежит на физический захват
        TaseringSuspect,      // primary применяет тайзер
        ExitingVehicleForCommand, // промежуточное: вышел из машины перед выполнением команды
        Treating,             // EMS: Treat Ped/Officer/Suspect
        LoadingPatient,
        Transporting,
        SeekingCover,         // под огнём, ищет укрытие
        InCover,
        CombatOpen,           // укрытия нет/не найдено, стреляет в открытую
        OpenFire,             // ручная команда U - стрельба в точку прицела игрока
        Downed
    }

    public enum WeaponSlotOption
    {
        CopyFromPlayer,
        Specific
    }

    public enum VehicleOption
    {
        CopyFromPlayer,
        Specific
    }
}
