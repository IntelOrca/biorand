using System;
using System.Collections.Generic;
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
            var entrance = doors[0];
            var exit = doors[1];

            var count = rng.Next(1, 4);
            var allies = Enumerable
                .Range(0, count)
                .Select(x => builder.AllocateAlly())
                .ToArray();
            var allyFlags = Enumerable
                .Range(0, count)
                .Select(x => builder.AllocateLocalFlag())
                .ToArray();

            var plotFlag = builder.AllocateGlobalFlag();
            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] ally pass by {{ flag {plotFlag.Flag} }}",
                        new SbIf(plotFlag, false,
                            new SbContainerNode(
                                allies.Select(x => new SbAlly(x, REPosition.OutOfBounds)).ToArray()),
                            new SbContainerNode(
                                allies.Select(x => new SbSetEntityCollision(x, false)).ToArray()),
                            new SbFork(
                                new SbProcedure(
                                    builder.CreateTrigger(doors[0].Cuts),
                                    new SbSetFlag(plotFlag),
                                    new SbLockPlot(
                                        new SbCommentNode($"[action] ally enter at {{ {doors[0]} }}",
                                            new SbContainerNode(
                                                allies
                                                    .Select(ally => DoAlly(builder, Array.IndexOf(allies, ally), allies.Length))
                                                    .ToArray()),
                                            new SbContainerNode(
                                                allyFlags.Select(x => new SbWaitForFlag(x.Flag)).ToArray())))))))));

            SbNode DoAlly(PlotBuilder builder, int index, int count)
            {
                var ally = allies[index];
                var flag = allyFlags[index];
                var result = new List<SbNode>();
                if (index == 0)
                {
                    result.Add(new SbDoor(entrance));
                }
                result.Add(new SbSleep(index * 15));
                result.Add(new SbMoveEntity(ally, entrance.Position));
                result.Add(new SbCommentNode($"[action] ally travel to {{ {exit} }}",
                    builder.Travel(ally, entrance, exit, PlcDestKind.Run, overrideDestination: exit.Position.Reverse())));
                result.Add(new SbMoveEntity(ally, REPosition.OutOfBounds));
                if (index == count - 1)
                {
                    result.Add(new SbDoor(exit));
                }
                result.Add(new SbSleep(4 * 30));
                result.Add(new SbSetFlag(flag));
                return new SbFork(new SbProcedure(result.ToArray()));
            }
        }
    }
}
