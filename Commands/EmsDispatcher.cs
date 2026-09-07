
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class EmsDispatcher
    {
        private const float TreatDurationMs = 5000f;

        public static void Treat(Unit unit, Ped patient)
        {
            if (unit == null ||
                patient == null ||
                !patient.Exists())
                return;

            if (unit.Status == UnitStatus.InVehicle)
            {
                unit.State = UnitState.ExitingVehicleForCommand;
                unit.PendingCommand = () => Treat(unit, patient);
                MovementDispatcher.ExitVehicle(unit);
                return;
            }

            unit.State = UnitState.Treating;
            unit.CurrentTarget = patient;

            unit.Ped.Tasks.FollowNavigationMeshToPosition(
                patient.Position,
                patient.Heading,
                1f);

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() &&
                       patient.Exists() &&
                       unit.Ped.Position.DistanceTo(patient.Position) > 1.5f &&
                       unit.State == UnitState.Treating)
                {
                    GameFiber.Sleep(100);
                }

                if (!unit.Ped.Exists() ||
                    !patient.Exists() ||
                    unit.State != UnitState.Treating)
                    return;

                NativeFunction.Natives.TASK_PLAY_ANIM(
                    unit.Ped,
                    "mini@cpr@char_a@cpr_str",
                    "cpr_pumpchest",
                    8f,
                    -8f,
                    (int)TreatDurationMs,
                    0,
                    0,
                    false,
                    false,
                    false);

                GameFiber.Sleep((int)TreatDurationMs);

                if (patient.Exists())
                {
                    if (patient.IsDead)
                        NativeFunction.Natives.RESURRECT_PED(patient);

                    patient.Health = patient.MaxHealth;
                }

                unit.State = UnitState.Idle;
            });
        }

        public static void LoadPatient(
            Unit emsUnit,
            Ped patient,
            Vehicle ambulance)
        {
            if (emsUnit == null ||
                patient == null ||
                !patient.Exists() ||
                ambulance == null ||
                !ambulance.Exists())
                return;

            emsUnit.State = UnitState.LoadingPatient;

            GameFiber.StartNew(() =>
            {
                const int RearSeat = 2;

                if (patient.IsDead ||
                    patient.Health < patient.MaxHealth * 0.3f)
                {
                    NativeFunction.Natives.SET_PED_INTO_VEHICLE(
                        patient,
                        ambulance,
                        RearSeat);
                }
                else
                {
                    patient.Tasks.EnterVehicle(
                        ambulance,
                        8000,
                        RearSeat);

                    GameFiber.Sleep(3000);
                }

                emsUnit.State = UnitState.Idle;
            });
        }

        public static void UnloadPatient(Ped patient)
        {
            if (patient == null || !patient.Exists())
                return;

            patient.Tasks.LeaveVehicle(
                LeaveVehicleFlags.None);
        }

        public static void TransportPatient(
            Unit driverUnit,
            Vector3 hospitalPosition)
        {
            if (driverUnit == null ||
                driverUnit.Vehicle == null ||
                !driverUnit.Vehicle.Exists())
                return;

            driverUnit.State = UnitState.Transporting;

            driverUnit.Ped.Tasks.DriveToPosition(
                driverUnit.Vehicle,
                hospitalPosition,
                20f,
                VehicleDrivingFlags.Emergency,
                5f);
        }
    }
}
