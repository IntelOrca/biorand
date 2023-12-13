namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class AnnouncerPlot : IPlot
    {
        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var plotFlag = builder.AllocateGlobalFlag();

            var player = builder.GetPlayer();
            var headMoveProc = new SbProcedure(
                new SbSleep(5),
                new SbMotion(player, 1, 1, 0),
                new SbSleep(5),
                new SbStopEntity(player),
                new SbSleep(10),
                new SbSetEntityNeck(player, 32, 0, -128),
                new SbSleep(40),
                new SbSetEntityNeck(player, 32, 512, -128),
                new SbSleep(40),
                new SbSetEntityNeck(player, 32, -512, -128));

            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] announcer {{ flag {plotFlag.Flag} }}",
                        new SbIf(plotFlag, false,
                            new SbFork(
                                new SbProcedure(
                                    builder.CreateTrigger(),
                                    new SbSetFlag(plotFlag),
                                    new SbLockPlot(
                                        new SbCutsceneBars(
                                            new SbFreezeAllEnemies(
                                                new SbFork(headMoveProc,
                                                    builder.Voice(new ICsHero[0], "announcer")),
                                                new SbSetEntityNeck(player, 32, 0, 0),
                                                new SbReleaseEntity(player))))))))));
        }
    }
}
