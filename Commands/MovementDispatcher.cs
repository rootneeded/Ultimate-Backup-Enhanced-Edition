
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class MovementDispatcher
    {
        private const VehicleDrivingFlags NormalDrivingFlags =
            VehicleDrivingFlags.Normal;

        public static void Go(
            IEnumerable<Unit> units,
            Vector3 destination)
        {
            foreach (var unit in units)
            {
                unit.State = UnitState.Idle;

                if (unit.Status == UnitStatus.InVehicle &&
                    unit.Vehicle != null &&
                    unit.Vehicle.Driver != null)
                {
                    unit.Vehicle.Driver.Tasks.DriveToPosition(
                        unit.Vehicle,
                        destination,
                        15f,
                        NormalDrivingFlags,
                        5f);
                }
                else
                {
                    unit.Ped.Tasks.FollowNavigationMeshToPosition(
                        destination,
                        0f,
                        3f);
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

                if (unit.Status == UnitStatus.InVehicle &&
                    unit.Vehicle != null &&
                    unit.Vehicle.Driver != null)
                {
                    unit.Vehicle.Driver.Tasks.CruiseWithVehicle(
                        unit.Vehicle,
                        15f,
                        NormalDrivingFlags);
                }
                else
                {
                    unit.Ped.Tasks.FollowToOffsetFromEntity(
                        player,
                        new Vector3(offset, -1.5f, 0f),
                        5f);
                }
            }
        }

        public static void Patrol(
            IEnumerable<Unit> units,
            float radiusMeters = 100f)
        {
            foreach (var unit in units)
            {
                unit.State = UnitState.Patrolling;

                unit.Ped.Tasks.Wander(
                    unit.Ped.Position,
                    radiusMeters);
            }
        }

        public static void Convoy(IList<Unit> carUnits)
        {
            var player = Game.LocalPlayer.Character;

            Entity previous =
                player.CurrentVehicle != null
                    ? (Entity)player.CurrentVehicle
                    : player;

            foreach (var unit in carUnits.Where(
                u => u.Status == UnitStatus.InVehicle &&
                     u.Vehicle != null &&
                     u.Vehicle.Driver != null))
            {
                unit.State = UnitState.Convoy;

                unit.Vehicle.Driver.Tasks.FollowToOffsetFromEntity(
                    previous,
                    new Vector3(0f, -6f, 0f),
                    20f);

                previous = unit.Vehicle;
            }
        }

        public static void Regroup(IEnumerable<Unit> units)
        {
            var player = Game.LocalPlayer.Character;

            foreach (var unit in units)
            {
                unit.State = UnitState.Idle;

                unit.Ped.Tasks.FollowNavigationMeshToPosition(
                    player.Position,
                    player.Heading,
                    2f);
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
                unit.CoverSearchStartTime =
                    Game.FrameTime.GetHashCode();
            }
        }

        public static void GetInVehicle(
            Unit unit,
            Vehicle targetVehicle = null)
        {
            var vehicle =
                targetVehicle ?? unit.AssignedVehicle;

            if (vehicle == null || !vehicle.Exists())
                return;

            bool isOwnVehicle =
                vehicle == unit.AssignedVehicle;

            int seat;

            if (isOwnVehicle)
            {
                seat = -1;
            }
            else
            {
                seat = FindAvailableSeat(vehicle);

                if (seat == -1)
                    return;
            }

            unit.Ped.Tasks.EnterVehicle(
                vehicle,
                -1,
                seat);

            unit.State = UnitState.Idle;
        }

        public static void ExitVehicle(Unit unit)
        {
            if (unit == null || unit.Ped == null)
                return;

            unit.Ped.Tasks.LeaveVehicle(
                LeaveVehicleFlags.None);

            unit.SetVehicle(null);
        }

        private static int FindAvailableSeat(
            Vehicle vehicle)
        {
            if (vehicle.IsSeatFree(0))
                return 0;

            if (vehicle.IsSeatFree(-1))
                return -1;

            if (vehicle.IsSeatFree(1))
                return 1;

            if (vehicle.IsSeatFree(2))
                return 2;

            return -1;
        }
    }
}

