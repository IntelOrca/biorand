using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class EnemyWalksInPlot : Plot, INewPlot
        {
            private const byte POSE_ZOMBIE_WAIT = 0;
            private const byte POSE_ZOMBIE_FOLLOW = 6;

            protected override void Build()
            {
                var previousEnemies = Builder.PlacedEnemyIds.ToArray();

                var typeMax = GetMaxEnemiesToWalkIn();
                var count = Cr.TakeEnemyCountForEvent(max: typeMax);
                var placements = Cr.GetEnemyPlacements(count);

                var door = GetRandomDoor()!;
                var enemyIds = Builder.AllocateEnemies(count);

                Builder.IfPlotTriggered();
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], placements[i]);
                    if (opcode.Type <= Re2EnemyIds.ZombieRandom)
                    {
                        // Zombie always in standing or walking pose
                        opcode.State = (byte)Cr._plotRng.NextOf(0, 6);
                    }
                    Builder.Enemy(opcode);
                }

                Builder.Else();
                foreach (var eid in enemyIds)
                {
                    var opcode = GenerateEnemy(eid, REPosition.OutOfBounds);
                    if (opcode.Type <= Re2EnemyIds.ZombieRandom)
                    {
                        // Zombie always in walking pose
                        opcode.State = 6;
                    }
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }

                Builder.BeginTriggerThread();
                AddTriggers(door.Cuts);

                // Move enemies into position and cut to them
                Builder.LockPlot();
                DoDoorOpenCloseCut(door);
                Builder.BeginCutsceneMode();

                LockEnemies(previousEnemies);

                foreach (var eid in enemyIds)
                {
                    var pos = door.Position + new REPosition(
                        Rng.Next(-50, 50),
                        0,
                        Rng.Next(-50, 50));
                    Builder.MoveEnemy(eid, pos);
                    Builder.ActivateEnemy(eid);
                }
                LogAction($"{enemyIds.Length}x enemy walk in");
                Builder.Sleep(60);
                UnlockEnemies(previousEnemies);
                Builder.CutRevert();
                Builder.EndCutsceneMode();
                Builder.SetFlag(Cr._plotFlag);

                // Delay next plot for at least 4s
                Builder.Sleep(30 * 4);

                Builder.UnlockPlot();
            }

            public CsPlot BuildPlot(PlotBuilder builder)
            {
                var previousEnemies = builder.GetEnemies();
                var typeMax = GetMaxEnemiesToWalkIn();
                var enemies = builder.AllocateEnemies(max: typeMax);
                var door = GetRandomDoor()!;
                var plotFlag = builder.AllocateGlobalFlag();

                var trigger = new SbProcedure(
                    builder.CreateTrigger(door.Cuts),
                    new SbSetFlag(plotFlag),
                    new SbLockPlot(
                        new SbCommentNode($"[action] {enemies.Length} enemies enter at {{ {door} }}",
                            new SbDoor(door),
                            new SbCutsceneBars(
                                new SbCut(door.Cut,
                                    new SbFreezeEnemies(previousEnemies,
                                        new SbContainerNode(
                                            enemies.Select(e =>
                                                new SbContainerNode(
                                                    new SbMoveEntity(e, GetEntryPosition(builder, door)),
                                                    new SbSetEntityEnabled(e, true))).ToArray()),
                                        new SbSleep(60))))),
                        new SbSleep(4 * 30)));

                var init = new SbProcedure(
                    new SbCommentNode($"[plot] {enemies.Length} enemies walk in",
                        new SbIf(plotFlag, false,
                            new SbContainerNode(
                                enemies.Select(e =>
                                    new SbEnemy(e,
                                        position: REPosition.OutOfBounds,
                                        pose: GetEnterEnemyPose(builder, e),
                                        enabled: false)).ToArray()),
                            new SbFork(trigger))
                        .Else(
                            new SbContainerNode(
                                enemies.Select(e => new SbEnemy(e,
                                    pose: GetEnterDefaultPose(builder, e))).ToArray()))));

                return new CsPlot(init);
            }

            private static REPosition GetEntryPosition(PlotBuilder builder, PointOfInterest door)
            {
                var rng = builder.Rng;
                var offset = new REPosition(
                    rng.Next(-50, 50),
                    0,
                    rng.Next(-50, 50));
                return door.Position + offset;
            }

            private static byte? GetEnterEnemyPose(PlotBuilder builder, CsEnemy enemy)
            {
                if (builder.EnemyHelper.IsZombie(enemy.Type))
                {
                    return POSE_ZOMBIE_FOLLOW;
                }
                return null;
            }

            private static byte? GetEnterDefaultPose(PlotBuilder builder, CsEnemy enemy)
            {
                if (builder.EnemyHelper.IsZombie(enemy.Type))
                {
                    return builder.Rng.NextOf(POSE_ZOMBIE_WAIT, POSE_ZOMBIE_FOLLOW);
                }
                return null;
            }

            private int GetMaxEnemiesToWalkIn()
            {
                var type = Cr._enemyType ?? Re2EnemyIds.ZombieRandom;
                switch (type)
                {
                    case Re2EnemyIds.ZombieDog:
                        return 4;
                    case Re2EnemyIds.Crow:
                        return 6;
                    case Re2EnemyIds.LickerRed:
                    case Re2EnemyIds.LickerGrey:
                        return 2;
                    case Re2EnemyIds.Spider:
                        return 3;
                    case Re2EnemyIds.GEmbryo:
                        return 8;
                    case Re2EnemyIds.Tyrant1:
                        return 4;
                    case Re2EnemyIds.Ivy:
                    case Re2EnemyIds.IvyPurple:
                        return 2;
                    case Re2EnemyIds.Birkin1:
                        return 1;
                    case Re2EnemyIds.GiantMoth:
                        return 1;
                    default:
                        return 4;
                }
            }
        }
    }
}
