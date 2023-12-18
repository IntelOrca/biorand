using System.Drawing;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class MurderPlot : IPlot
    {
        private Color ShadowColour => Color.FromArgb(239, 64, 64);

        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var rng = builder.Rng;
            var exit = builder.PoiGraph.GetRandomPoi(rng, x => x.HasTag("door"));
            if (exit == null)
                return null;

            var spot = builder.PoiGraph.GetRandomPoi(rng, x => x.HasTag("meet"));
            if (spot == null)
                return null;

            var allyMurder = builder.AllocateAlly();
            var allyVictim = builder.AllocateAlly();

            var plotFlag = builder.AllocateGlobalFlag();

            var msg = builder.AllocateMessage(
                string.Format(rng.NextOf(_inspectionMessages), allyVictim.DisplayName, allyMurder.DisplayName),
                autoBreak: true);
            var interaction = builder.AllocateEvent();

            var setupVictim = new SbProcedure(
                new SbSleep(1),
                new SbEnableEvent(interaction,
                    new SbProcedure(
                        new SbMessage(msg))),
                new SbMoveEntity(allyVictim, spot.Position),
                new SbKageSet(allyVictim, ShadowColour, 1500),
                new SbMotion(allyVictim, 1, 2, 192),
                new SbSleep(1),
                new SbStopEntity(allyVictim));

            var trigger = new SbProcedure(
                builder.CreateTrigger(
                    notCuts: spot.AllCuts,
                    minSleepTime: 1,
                    maxSleepTime: 1,
                    allowCutTrigger: false),
                new SbLockPlot(
                    new SbSetFlag(plotFlag, true),
                    new SbMoveEntity(allyMurder, spot.Position),
                    new SbCall(setupVictim),
                    new SbFreezeAllEnemies(
                        new SbCutsceneBars(
                            builder.Voice(new[] { allyVictim }, new[] { allyVictim }, "doom", trailingSilence: false),
                            builder.SoundEffect("gunshot"),
                            new SbSleep(30))),
                    new SbWaitForCut(spot.AllCuts),
                    builder.Travel(allyMurder, spot, exit, PlcDestKind.Run, exit.Position.Reverse()),
                    new SbCommentNode($"[action] ally leave at {{ {exit} }}",
                        new SbMoveEntity(allyMurder, REPosition.OutOfBounds),
                        new SbDoor(exit)))); ;

            return new CsPlot(
                new SbProcedure(
                    new SbCommentNode($"[plot] murder {{ flag {plotFlag.Flag} }}",
                        new SbAlly(allyVictim, REPosition.OutOfBounds),
                        new SbAlly(allyMurder, REPosition.OutOfBounds),
                        new SbSetEntityCollision(allyVictim, false),
                        new SbAot(interaction, spot.Position, 2000),
                        new SbIf(plotFlag, false,
                            new SbFork(trigger)
                        ).Else(
                            new SbFork(setupVictim)))));
        }

        private readonly string[] _inspectionMessages = new[]
        {
            "Poor {0}.",
            "{0} has been brutally murdered.",
            "{0} has nothing of value.",
            "Good riddance.",
            "Rest in peace.",
            "Press 'F' to pay respects.",
            "I promise to avenge you.",
            "{1} will pay for this!",
            "{1} was behind this!",
            "{1} is a murderer!"
        };
    }
}
