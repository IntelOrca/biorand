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
            var doors = doorGroup.Shuffle(rng).Take(2).ToArray();
            var ally = builder.AllocateAlly();
            var plotFlag = builder.AllocateGlobalFlag();
            return new CsPlot(
                new SbProcedure(
                    new SbIf(plotFlag, false,
                        new SbAlly(ally, REPosition.OutOfBounds),
                        new SbSetEntityCollision(ally, false),
                        new SbFork(
                            new SbProcedure(
                                builder.CreateTrigger(doors[0].Cuts),
                                new SbSetFlag(plotFlag),
                                new SbLockPlot(
                                    new SbDoor(doors[0]),
                                    new SbMoveEntity(ally, doors[0].Position),
                                    builder.Travel(ally, doors[0], doors[1], PlcDestKind.Run, overrideDestination: doors[1].Position.Reverse()),
                                    new SbMoveEntity(ally, REPosition.OutOfBounds),
                                    new SbDoor(doors[1]),
                                    new SbSleep(4 * 30)))))));
        }
    }
}
