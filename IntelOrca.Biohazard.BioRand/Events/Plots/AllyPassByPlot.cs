using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class AllyPassByPlot : IPlot
    {
        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var rng = builder.Rng;
            var connectedDoorGraph = builder.PoiGraph.GetGraphsContaining(PoiKind.Door, PoiKind.Door);
            if (connectedDoorGraph.Length == 0)
                return null;

            var doorGroup = rng.NextOf(connectedDoorGraph);
            var doors = doorGroup
                .Where(x => x.HasTag("door"))
                .Shuffle(rng)
                .Take(2)
                .ToArray();
            var ally = builder.AllocateAlly();
            var plotFlag = builder.AllocateGlobalFlag();
            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] ally pass by {{ flag {plotFlag.Flag} }}",
                        new SbIf(plotFlag, false,
                            new SbAlly(ally, REPosition.OutOfBounds),
                            new SbSetEntityCollision(ally, false),
                            new SbFork(
                                new SbProcedure(
                                    builder.CreateTrigger(doors[0].Cuts),
                                    new SbSetFlag(plotFlag),
                                    new SbLockPlot(
                                        new SbCommentNode($"[action] ally enter at {{ {doors[0]} }}",
                                            new SbDoor(doors[0]),
                                            // new SbWaitForCut(doors[0].Cuts, false),
                                            new SbMoveEntity(ally, doors[0].Position),
                                            new SbCommentNode($"[action] ally travel to {{ {doors[1]} }}",
                                                builder.Travel(ally, doors[0], doors[1], PlcDestKind.Run, overrideDestination: doors[1].Position.Reverse())),
                                            new SbMoveEntity(ally, REPosition.OutOfBounds),
                                            new SbDoor(doors[1]),
                                            new SbSleep(4 * 30)))))))));
        }
    }
}
