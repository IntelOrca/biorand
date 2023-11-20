using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class AllyPassByPlot : Plot
        {
            protected override bool Check()
            {
                var groups = GetConnectedDoors();
                return groups.Length != 0;
            }

            protected override void Build()
            {
                var doorGroup = Rng.NextOf(GetConnectedDoors());
                var doors = doorGroup.Shuffle(Rng).Take(2).ToArray();

                var npcId = Builder.AllocateEnemies(1).FirstOrDefault();
                var enemyType = Re2EnemyIds.ClaireRedfield;
                var npcRando = Cr._npcRandomiser;
                if (npcRando != null)
                {
                    enemyType = npcRando.GetRandomNpc(Cr._rdt!, Rng);
                }

                Builder.IfPlotTriggered();
                Builder.Else();
                Builder.Ally(npcId, enemyType, REPosition.OutOfBounds);
                Builder.DisableEnemyCollision(npcId);

                Builder.BeginTriggerThread();
                AddTriggers(doors[0].Cuts);

                Builder.SetFlag(Cr._plotId >> 8, Cr._plotId & 0xFF);

                // Move ally into position and cut to them
                Builder.LockPlot();
                DoDoorOpenClose(doors[0]);
                Builder.MoveEnemy(npcId, doors[0].Position);

                IntelliTravelTo(24, npcId, doors[0], doors[1], null, PlcDestKind.Run);
                Builder.WaitForFlag(CutsceneBuilder.FG_ROOM, 24);

                Builder.MoveEnemy(npcId, REPosition.OutOfBounds);
                DoDoorOpenClose(doors[1]);
                Builder.Sleep(30 * 4);
                Builder.UnlockPlot();

                LogAction($"Ally runs from door {doors[0].Id} to door {doors[1].Id}");
            }

            private PointOfInterest[][] GetConnectedDoors()
            {
                var doors = Cr._poi.Where(x => x.HasTag(PoiKind.Door)).ToArray();
                if (doors.Length < 2)
                {
                    return new PointOfInterest[0][];
                }

                var result = new List<PointOfInterest[]>();
                var seen = new HashSet<PointOfInterest>();
                foreach (var door in doors)
                {
                    if (seen.Contains(door))
                        continue;

                    var group = GetAllConnected(door)
                        .Where(x => x.HasTag(PoiKind.Door))
                        .ToArray();
                    if (group.Length >= 2)
                    {
                        result.Add(group);
                    }
                    foreach (var d in group)
                    {
                        seen.Add(d);
                    }
                }
                return result.ToArray();
            }
        }
    }
}
