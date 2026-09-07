
using System.Collections.Generic;
using Rage;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class GuardDispatcher
    {
        private const float GuardOffset = 1.8f;
        private const float FleeCheckDistance = 6f;

        public static void GuardSuspect(Unit unit, Ped suspect)
        {
            if (unit == null ||
                suspect == null ||
                !suspect.Exists())
                return;

            unit.State = UnitState.Guarding;
            unit.CurrentTarget = suspect;

            GameFiber.StartNew(() =>
            {
                while (unit.Ped.Exists() &&
                       suspect.Exists() &&
                       unit.State == UnitState.Guarding)
                {
                    float dist =
                        unit.Ped.Position.DistanceTo(
                            suspect.Position);

                    if (dist > FleeCheckDistance)
                    {
                        unit.Ped.Tasks.GoStraightToPosition(
                            suspect.Position,
                            3f,
                            suspect.Heading);
                    }
                    else if (dist > GuardOffset + 0.5f)
                    {
                        unit.Ped.Tasks.FollowNavigationMeshToPosition(
                            suspect.Position,
                            suspect.Heading,
                            GuardOffset);
                    }

                    GameFiber.Sleep(500);
                }
            });
        }

        public static void CoverMe(IEnumerable<Unit> units)
        {
            var player = Game.LocalPlayer.Character;

            foreach (var unit in units)
            {
                unit.State = UnitState.Following;

                unit.Ped.Tasks.FollowToOffsetFromEntity(
                    player,
                    new Vector3(-1.5f, -1.5f, 0f),
                    5f);
            }
        }
    }
}

