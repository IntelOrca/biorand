using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class EnemyWakeUpPlot : Plot, INewPlot
        {
            private const byte POSE_ZOMBIE_WAIT = 0;
            private const byte POSE_ZOMBIE_LYING = 1;
            private const byte POSE_ZOMBIE_WAKE_UP = 2;
            private const byte POSE_ZOMBIE_CRAWL = 3;
            private const byte POSE_ZOMBIE_GET_UP = 4;
            private const byte POSE_ZOMBIE_DEAD_UP = 5;
            private const byte POSE_ZOMBIE_FOLLOW = 6;
            private const byte POSE_ZOMBIE_DEAD = 7;
            private const byte POSE_ZOMBIE_EATING = 8;
            private const byte POSE_ZOMBIE_40 = 0x40;

            protected override void Build()
            {
                var count = Cr.TakeEnemyCountForEvent();
                var placements = Cr.GetEnemyPlacements(count);
                var ids = Builder.AllocateEnemies(count);

                Builder.IfPlotTriggered();
                // Setup enemies in woken up positions
                for (int i = 0; i < count; i++)
                {
                    var opcode = GenerateEnemy(ids[i], placements[i]);
                    Builder.Enemy(opcode);
                }

                Builder.Else();

                // Setup initial enemy positions
                for (int i = 0; i < count; i++)
                {
                    var opcode = GenerateEnemy(ids[i], placements[i]);
                    opcode.State = 4;
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }
                for (var i = 0; i < count; i++)
                {
                    Builder.DisableEnemyCollision(ids[i]);
                }

                // Wait for triggers
                Builder.BeginTriggerThread();
                AddTriggers();

                // Wake up enemies incrementally
                Builder.LockPlot();
                foreach (var eid in ids)
                {
                    Builder.Sleep(Rng.Next(5, 15));
                    Builder.EnableEnemyCollision(eid);
                    Builder.ActivateEnemy(eid);
                }
                Builder.UnlockPlot();
                LogAction($"{ids.Length}x enemy wake up");
            }

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
                                    new SbSleep(Rng.Next(5, 15)),
                                    new SbSetEntityCollision(x, true),
                                    new SbSetEntityEnabled(x, true))).ToArray())));

                var init = new SbProcedure(
                    new SbCommentNode($"[plot] {enemies.Length} sleeping enemies wake up",
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

                return new CsPlot(init);
            }
        }
    }
}
