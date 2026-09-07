
using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Compatibility;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class SpawnDispatcher
    {
        private static readonly Random Rnd = new Random();

        private const float MinSpawnDistance = 120f;
        private const float MaxSpawnDistance = 260f;
        private const float ArrivalThreshold = 8f;

        private static readonly Dictionary<UnitType, (string VehicleModel, int Occupants, bool IsVan)> Composition =
            new Dictionary<UnitType, (string, int, bool)>
            {
                { UnitType.LspdPatrol,  ("police",    2, false) },
                { UnitType.LssdPatrol,  ("sheriff",   2, false) },
                { UnitType.StatePatrol, ("police3",   2, false) },
                { UnitType.Fib,         ("fbi",       2, false) },
                { UnitType.FibHrt,      ("fbi2",      4, true)  },
                { UnitType.Swat,        ("riot",      8, true)  },
                { UnitType.Noose,       ("riot",      8, true)  },
                { UnitType.NooseSwat,   ("riot2",     8, true)  },
                { UnitType.Ems,         ("ambulance", 2, true)  },
            };

        public static void Dispatch(UnitType type, UnitSource source)
        {
            Vector3 destination = Game.LocalPlayer.Character.Position;

            var waypoint = World.GetWaypointBlip();
            if (waypoint != null && waypoint.Exists())
                destination = waypoint.Position;

            if (!Composition.TryGetValue(type, out var comp))
                return;

            GameFiber.StartNew(() =>
                SpawnAndDrive(
                    type,
                    source,
                    comp.VehicleModel,
                    comp.Occupants,
                    comp.IsVan,
                    destination));
        }

        private static void SpawnAndDrive(
            UnitType type,
            UnitSource source,
            string modelName,
            int occupants,
            bool isVan,
            Vector3 destination)
        {
            Vector3 spawnPoint = FindSpawnPointOutOfSight(destination);
            if (spawnPoint == Vector3.Zero)
                return;

            var model = new Model(modelName);
            model.LoadAndWait();

            var vehicle = new Vehicle(model, spawnPoint);

            bool useSiren = source == UnitSource.Code3;
            vehicle.IsSirenOn = useSiren;

            var driver = CreatePedInSeat(vehicle, -1, "s_m_y_cop_01");
            if (driver == null || !driver.Exists())
                return;

            LspdfrBridge.SetCopAsBusy(driver, true);

            var driverUnit = new Unit(driver, source, type)
            {
                Lifecycle = UnitLifecycle.EnRoute
            };

            driverUnit.SetVehicle(vehicle);
            UnitManager.Register(driverUnit);

            int maxPassengers =
                NativeFunction.Natives.GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS<int>(vehicle);

            int passengerCount = Math.Min(
                Math.Max(occupants - 1, 0),
                maxPassengers);

            var passengers = new List<Unit>();

            for (int seatIndex = 0; seatIndex < passengerCount; seatIndex++)
            {
                var ped = CreatePedInSeat(
                    vehicle,
                    seatIndex,
                    "s_m_y_cop_01");

                if (ped == null || !ped.Exists())
                    continue;

                LspdfrBridge.SetCopAsBusy(ped, true);

                var unit = new Unit(ped, source, type)
                {
                    Lifecycle = UnitLifecycle.EnRoute
                };

                unit.SetVehicle(vehicle);
                UnitManager.Register(unit);
                passengers.Add(unit);
            }

            var drivingFlags = useSiren
                ? VehicleDrivingFlags.Emergency
                : VehicleDrivingFlags.Normal;

            driver.Tasks.DriveToPosition(
                vehicle,
                destination,
                useSiren ? 25f : 15f,
                drivingFlags,
                ArrivalThreshold);

            while (vehicle.Exists() &&
                   vehicle.Position.DistanceTo(destination) > ArrivalThreshold)
            {
                GameFiber.Sleep(500);
            }

            if (!vehicle.Exists())
                return;

            driverUnit.Lifecycle = UnitLifecycle.Arrived;

            foreach (var passenger in passengers)
                passenger.Lifecycle = UnitLifecycle.Arrived;

            NativeFunction.Natives.TASK_VEHICLE_PARK(
                driver,
                vehicle,
                vehicle.Position.X,
                vehicle.Position.Y,
                vehicle.Heading,
                1,
                20f,
                false);

            GameFiber.Sleep(1500);

            if (isVan)
            {
                driverUnit.Lifecycle = UnitLifecycle.Unloading;
                driver.Tasks.LeaveVehicle(LeaveVehicleFlags.None);

                foreach (var passenger in passengers)
                {
                    passenger.Lifecycle = UnitLifecycle.Unloading;
                    passenger.Ped.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                }

                GameFiber.Sleep(2000);
            }

            driverUnit.Lifecycle = UnitLifecycle.Active;
            driverUnit.State = UnitState.Idle;

            foreach (var passenger in passengers)
            {
                passenger.Lifecycle = UnitLifecycle.Active;
                passenger.State = UnitState.Idle;
            }
        }

        private static Ped CreatePedInSeat(
            Vehicle vehicle,
            int seatIndex,
            string modelName)
        {
            var model = new Model(modelName);
            model.LoadAndWait();

            var ped = new Ped(
                model,
                vehicle.Position,
                vehicle.Heading);

            NativeFunction.Natives.SET_PED_INTO_VEHICLE(
                ped,
                vehicle,
                seatIndex);

            return ped;
        }

        private static Vector3 FindSpawnPointOutOfSight(Vector3 destination)
        {
            var player = Game.LocalPlayer.Character;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle =
                    (float)(Rnd.NextDouble() * 360.0);

                float dist =
                    MinSpawnDistance +
                    (float)Rnd.NextDouble() *
                    (MaxSpawnDistance - MinSpawnDistance);

                float rad =
                    angle * (float)(Math.PI / 180.0);

                var candidate =
                    destination +
                    new Vector3(
                        (float)Math.Cos(rad) * dist,
                        (float)Math.Sin(rad) * dist,
                        0f);

                Vector3 roadPos;
                float roadHeading;

                bool found =
                    NativeFunction.Natives
                        .GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                            candidate.X,
                            candidate.Y,
                            candidate.Z,
                            out roadPos,
                            out roadHeading,
                            1,
                            3.0f,
                            0);

                if (!found)
                    continue;

                bool hasLos =
                    World.TraceLine(
                        player.Position + new Vector3(0, 0, 0.6f),
                        roadPos + new Vector3(0, 0, 0.6f),
                        TraceFlags.IntersectWorld).Hit;

                if (hasLos)
                    continue;

                return roadPos;
            }

            return destination +
                   new Vector3(MinSpawnDistance, 0, 0);
        }
    }
}
