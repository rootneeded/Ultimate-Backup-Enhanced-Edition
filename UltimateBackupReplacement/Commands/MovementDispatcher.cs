using System.Collections.Generic;
using System.Linq;
using Rage;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class MovementDispatcher
    {
        private const float PatrolDefaultRadius = 100f;
        private const VehicleDrivingFlags NormalDrivingFlags = VehicleDrivingFlags.Normal;

        public static void Go(IEnumerable<Unit> units, Vector3 destination)
        {
            foreach (var unit in units)
            {
                unit.State = UnitState.Idle; // сброс перед новой задачей
                if (unit.Status == UnitStatus.InVehicle && unit.Vehicle != null && unit.Vehicle.Driver != null)
                {
                    unit.Vehicle.Driver.Tasks.DriveToPosition(unit.Vehicle, destination, 15f, NormalDrivingFlags, 5f);
                }
                else
                {
                    unit.Ped.Tasks.FollowNavigationMeshToPosition(destination, 0f, 3f);
                }
            }
        }

        public static void FollowMe(IEnumerable<Unit> units)
        {
            var player = Game.LocalPlayer.Character;
            int i = 0;
            foreach (var unit in units)
            {
                unit.State = UnitState.Following;
                float offset = 1.5f * (i++ + 1);

                if (unit.Status == UnitStatus.InVehicle && unit.Vehicle != null && unit.Vehicle.Driver != null)
                {
                    // CarUnit едет за игроком на машине
                    unit.Vehicle.Driver.Tasks.CruiseWithVehicle(unit.Vehicle, 15f, NormalDrivingFlags);
                    // Более точное следование лучше реализовать собственным Tick-based
                    // пересчётом целевой точки за игроком (offset позади машины игрока).
                }
                else
                {
                    unit.Ped.Tasks.FollowToOffsetFromEntity(player, new Vector3(offset, -1.5f, 0f), 5f, -1, 3f, true);
                }
            }
        }

        public static void Patrol(IEnumerable<Unit> units, float radiusMeters = PatrolDefaultRadius)
        {
            foreach (var unit in units)
            {
                unit.State = UnitState.Patrolling;
                unit.Ped.Tasks.WanderAround(unit.Ped.Position, radiusMeters);
            }
        }

        /// <summary>
        /// Каждая машина следует не за игроком напрямую, а за ПРЕДЫДУЩЕЙ машиной в колонне
        /// (первая - за игроком). Это классическая leader-follower цепочка - она нормально
        /// проходит повороты, в отличие от фиксированного offset от игрока для всех машин,
        /// который на поворотах сводит все машины в одну точку.
        /// </summary>
        public static void Convoy(IList<Unit> carUnits)
        {
            var player = Game.LocalPlayer.Character;
            Entity previous = player.CurrentVehicle != null ? (Entity)player.CurrentVehicle : player;

            foreach (var unit in carUnits.Where(u => u.Status == UnitStatus.InVehicle && u.Vehicle?.Driver != null))
            {
                unit.State = UnitState.Convoy;
                unit.Vehicle.Driver.Tasks.FollowToOffsetFromEntity(
                    previous, new Vector3(0f, -6f, 0f), 20f, -1, 4f, true);

                previous = unit.Vehicle; // следующая машина в колонне едет уже за этой
            }
        }

        public static void Regroup(IEnumerable<Unit> units)
        {
            var player = Game.LocalPlayer.Character;
            foreach (var unit in units)
            {
                unit.State = UnitState.Idle;
                unit.Ped.Tasks.FollowNavigationMeshToPosition(player.Position, player.Heading, 2f);
            }
        }

        public static void HoldPosition(IEnumerable<Unit> units)
        {
            foreach (var unit in units)
            {
                unit.State = UnitState.Idle;
                unit.Ped.Tasks.Clear();
                unit.Ped.Tasks.StandStill(-1);
            }
        }

        public static void TakeCover(IEnumerable<Unit> units)
        {
            foreach (var unit in units)
            {
                unit.State = UnitState.SeekingCover;
                unit.CoverSearchStartTime = Game.FrameTime.GetHashCode(); // заменить на реальный таймстамп
                // Реальный поиск cover-point выполняется через CombatDispatcher
                // при активном State == SeekingCover (см. CombatDispatcher.UpdateCoverBehavior).
            }
        }

        /// <summary>
        /// Get In Vehicle. Если targetVehicle не указан (null) - используется AssignedVehicle партнёра.
        /// Партнёр в свою AssignedVehicle всегда садится за руль.
        /// В чужую (например машину игрока) - по приоритету Front Passenger -> Driver -> Rear.
        /// </summary>
        public static void GetInVehicle(Unit unit, Vehicle targetVehicle = null)
        {
            var vehicle = targetVehicle ?? unit.AssignedVehicle;
            if (vehicle == null || !vehicle.Exists()) return;

            bool isOwnVehicle = vehicle == unit.AssignedVehicle;

            if (isOwnVehicle)
            {
                unit.Ped.Tasks.EnterVehicle(vehicle, -1, (int)VehicleSeat.Driver);
            }
            else
            {
                var seat = FindAvailableSeat(vehicle);
                if (seat == VehicleSeat.Driver && vehicle.Driver != null && vehicle.Driver.Exists())
                {
                    // вытеснить NPC с водительского места
                    vehicle.Driver.Tasks.LeaveVehicle(LeaveVehicleFlags.WarpOut);
                }
                unit.Ped.Tasks.EnterVehicle(vehicle, -1, (int)seat);
            }

            unit.State = UnitState.Idle;
        }

        public static void ExitVehicle(Unit unit)
        {
            unit.Ped.Tasks.LeaveVehicle();
            unit.SetVehicle(null);
        }

        private static VehicleSeat FindAvailableSeat(Vehicle vehicle)
        {
            if (vehicle.PassengerSeats > 0 && !vehicle.IsSeatFree(VehicleSeat.Front) == false)
                return VehicleSeat.Front;
            if (vehicle.Driver == null)
                return VehicleSeat.Driver;
            return VehicleSeat.RightRear; // упрощённо; реально нужно перебрать все rear-сиденья
        }
    }
}
