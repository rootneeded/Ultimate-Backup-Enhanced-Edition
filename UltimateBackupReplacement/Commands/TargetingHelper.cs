using Rage;
using UltimateBackupReplacement.Compatibility;

namespace UltimateBackupReplacement.Commands
{
    /// <summary>Общая логика получения "на что сейчас указывает игрок" - используется и биндами (O/U),
    /// и кнопками команд в UI планшета (Stop Ped/Arrest/Search и т.п.), чтобы не дублировать raycast.</summary>
    public static class TargetingHelper
    {
        public static Ped GetAimedOrNearestPed(float fallbackRadius = 15f)
        {
            var player = Game.LocalPlayer.Character;
            if (player.IsAimingWithGamepad || player.IsAiming)
            {
                int handle = NativeFunction.Natives.GET_PED_TARGET_FROM_LOCAL_PLAYER<int>(player);
                var entity = Rage.Entity.FromHandle(handle);
                if (entity is Ped p && p.Exists()) return p;
            }
            return World.GetClosestPed(player.Position, fallbackRadius);
        }

        public static Vehicle GetAimedOrNearestVehicle(float fallbackRadius = 15f)
        {
            var point = GetAimPoint();
            return World.GetClosestVehicle(point, fallbackRadius);
        }

        public static Vector3 GetAimPoint(float maxDistance = 100f)
        {
            var camPos = GameplayCamera.Position;
            var camDir = GameplayCamera.Direction;
            var result = World.TraceLine(camPos, camPos + camDir * maxDistance, TraceFlags.IntersectWorld | TraceFlags.IntersectPeds);
            return result.Hit ? result.HitPosition : camPos + camDir * (maxDistance * 0.5f);
        }

        /// <summary>
        /// Единая проверка перед Arrest/Force Arrest/Search Ped/Stop Ped/Request*: цель должна
        /// существовать и быть жива, НЕ вестись прямо сейчас модом Stop The Ped (иначе два мода
        /// дёргают Task одного ped'а одновременно), и НЕ быть другим офицером полиции по мнению
        /// LSPDFR (защита от случайного захвата aimed/nearest ped'а не того, кого игрок хотел -
        /// например если рядом с подозреваемым стоит свой же коп).
        /// </summary>
        public static bool IsValidSuspectTarget(Ped ped)
        {
            if (ped == null || !ped.Exists() || ped.IsDead) return false;
            if (StopThePedCompatibility.IsPedBeingHandled(ped)) return false;
            if (LspdfrBridge.IsPedAPoliceOfficer(ped)) return false;
            return true;
        }
    }
}
