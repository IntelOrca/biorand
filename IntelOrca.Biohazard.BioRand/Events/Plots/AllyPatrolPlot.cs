using System;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class AllyPatrolPlot : IPlot
    {
        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var rng = builder.Rng;
            var randomPoi = builder.PoiGraph.GetRandomPoi(rng, _ => true);
            if (randomPoi == null)
                return null;

            var graph = builder.PoiGraph.GetGraph(randomPoi);
            if (graph.Length < 3)
                return null;

            var patrolCount = rng.Next(3, Math.Min(6, graph.Length));
            var patrolPoi = graph.Shuffle(rng).Take(patrolCount).ToArray();
            var ally = builder.AllocateAlly();
            var completeFlag = builder.AllocateLocalFlag();
            var subCompleteFlag = builder.AllocateLocalFlag();

            var travelRoute = new SbNodeBuilder();
            for (var i = 0; i < patrolPoi.Length; i++)
            {
                var lastPoi = patrolPoi[i == 0 ? patrolPoi.Length - 1 : i - 1];
                var poi = patrolPoi[i];
                var pos = poi.Position;

                travelRoute.Append(builder.Travel(ally, lastPoi, poi, PlcDestKind.Walk,
                    strict: false,
                    completeFlag: completeFlag,
                    subCompleteFlag: subCompleteFlag));
                travelRoute.Append(MoveHeadAbout(ally, rng));
            }
            travelRoute.Reparent(x => new SbLoop(x));
            travelRoute.Prepend(new SbSleep(30));

            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] ally patrol",
                        builder.CreateEnemyConditionGuard(
                            new SbAlly(ally, patrolPoi.Last().Position),
                            new SbFork(new SbProcedure(travelRoute.Build()))))));
        }

        private static SbNode MoveHeadAbout(ICsHero entity, Rng rng)
        {
            var speed = rng.Next(8, 32);
            var count = rng.Next(3, 8);
            var result = new SbNodeBuilder();
            for (var i = 0; i < count; i++)
            {
                var x = rng.Next(-512, 512);
                var y = rng.Next(-256, 32);
                var sleep = rng.Next(30, 90);

                result.Append(new SbSetEntityNeck(entity, speed, x, y));
                result.Append(new SbSleep(sleep));
            }
            result.Append(new SbSetEntityNeck(entity, speed, 0, 0));
            return result.Build();
        }
    }
}
