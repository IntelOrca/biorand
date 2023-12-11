using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class EnemyWakeUpPlot : IPlot
    {
        private const byte POSE_ZOMBIE_WAIT = 0;
        private const byte POSE_ZOMBIE_CRAWL = 3;
        private const byte POSE_ZOMBIE_GET_UP = 4;
        private const byte POSE_ZOMBIE_FOLLOW = 6;

        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var enemies = builder.AllocateEnemies();
            var plotFlag = builder.AllocateGlobalFlag();

            var trigger = new SbProcedure(
                builder.CreateTrigger(),
                new SbSetFlag(plotFlag),
                new SbLockPlot(
                    new SbCommentNode($"[action] wake up {enemies.Length} enemies",
                        enemies.Select(x =>
                            new SbContainerNode(
                                new SbSleep(builder.Rng.Next(5, 15)),
                                new SbSetEntityCollision(x, true),
                                new SbSetEntityEnabled(x, true))).ToArray())));

            var init = new SbProcedure(
                new SbCommentNode($"[plot] {enemies.Length} sleeping enemies wake up {{ flag {plotFlag.Flag} }}",
                    new SbIf(plotFlag, false,
                        new SbContainerNode(
                            enemies.Select(e =>
                                new SbContainerNode(
                                    new SbEnemy(e,
                                        enabled: false,
                                        pose: builder.Rng.NextOf(POSE_ZOMBIE_GET_UP, POSE_ZOMBIE_CRAWL)),
                                    new SbSetEntityCollision(e, false))).ToArray()),
                        new SbFork(trigger))
                    .Else(
                        new SbContainerNode(
                            enemies.Select(x => new SbEnemy(x,
                                pose: builder.Rng.NextOf(POSE_ZOMBIE_WAIT, POSE_ZOMBIE_FOLLOW))).ToArray()))));

            return new CsPlot(init, endOfScript: true);
        }
    }
}
