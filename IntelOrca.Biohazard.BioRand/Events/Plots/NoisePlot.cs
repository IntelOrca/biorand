namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class NoisePlot : IPlot
    {
        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var rng = builder.Rng;
            var door = builder.PoiGraph.GetRandomPoi(rng, x => x.HasTag("door"));
            if (door == null)
                return null;

            var plotFlag = builder.AllocateGlobalFlag();
            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] noise {{ flag {plotFlag.Flag} }}",
                        new SbIf(plotFlag, false,
                            new SbFork(
                                new SbProcedure(
                                    builder.CreateTrigger(),
                                    new SbSetFlag(plotFlag),
                                    new SbLockPlot(
                                        new SbCommentNode($"[action] noise heard at {{ {door} }}",
                                            new SbCutsceneBars(
                                                new SbFreezeAllEnemies(
                                                    new SbCut(door.Cut,
                                                        builder.Voice(new ICsHero[0], "jumpscare"))))))))))));
        }
    }
}
