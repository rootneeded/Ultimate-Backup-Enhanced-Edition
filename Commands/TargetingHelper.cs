
using Rage;
using UltimateBackupReplacement.Compatibility;

namespace UltimateBackupReplacement.Commands
{
    /// <summary>
    /// Общая логика получения цели, на которую указывает игрок.
    /// Совместимо с текущим RagePluginHook.dll.
    /// </summary>
    public static class TargetingHelper
    {
        public static Ped GetAimedOrNearestPed(float fallbackRadius = 15f)
        {
            var player = Game.LocalPlayer.Character;

            if (player.IsAiming)
            {
                int handle = NativeFunction.Natives.GET_PED_TARGET_FROM_LOCAL_PLAYER<int>();

                if (handle != 0)
                {
                    var ped = World.GetEntityByHandle<Ped>(handle);

                    if (ped != null && ped.Exists())
                        return ped;
                }
            }

            var entity = World.GetClosestEntity(
                player.Position,
                fallbackRadius,
                GetEntitiesFlags.Peds);

            return entity as Ped;
        }

        public static Vehicle GetAimedOrNearestVehicle(float fallbackRadius = 15f)
        {
            var point = GetAimPoint();

            var entity = World.GetClosestEntity(
                point,
                fallbackRadius,
                GetEntitiesFlags.Vehicles);

            return entity as Vehicle;
        }

        public static Vector3 GetAimPoint(float maxDistance = 100f)
        {
            var camera = Camera.RenderingCamera;

            if (camera == null)
            {
                var player = Game.LocalPlayer.Character;
                return player.Position + player.Direction * (maxDistance * 0.5f);
            }

            var camPos = camera.Position;
            var camDir = camera.Direction;

            var result = World.TraceLine(
                camPos,
                camPos + camDir * maxDistance,
                TraceFlags.IntersectWorld | TraceFlags.IntersectPeds);

            return result.Hit
                ? result.HitPosition
                : camPos + camDir * (maxDistance * 0.5f);
        }

        /// <summary>
        /// Единая проверка перед Arrest/Force Arrest/Search Ped/Stop Ped/Request.
        /// </summary>
        public static bool IsValidSuspectTarget(Ped ped)
        {
            if (ped == null || !ped.Exists() || ped.IsDead)
                return false;

            if (StopThePedCompatibility.IsPedBeingHandled(ped))
                return false;

            if (LspdfrBridge.IsPedAPoliceOfficer(ped))
                return false;

            return true;
        }
    }
}

