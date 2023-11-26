using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class EnemyWalksInPlot : IPlot
    {
        private const byte POSE_ZOMBIE_WAIT = 0;
        private const byte POSE_ZOMBIE_FOLLOW = 6;

        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var typeMax = GetMaxEnemiesToWalkIn(builder);
            var enemies = builder.AllocateEnemies(max: typeMax);
            var door = builder.PoiGraph.GetRandomDoor(builder.Rng);
            if (door == null)
                return null;

            var plotFlag = builder.AllocateGlobalFlag();

            var trigger = new SbProcedure(
                builder.CreateTrigger(door.Cuts),
                new SbSetFlag(plotFlag),
                new SbLockPlot(
                    new SbCommentNode($"[action] {enemies.Length} enemies enter at {{ {door} }}",
                        new SbDoor(door),
                        new SbFreezeAllEnemies(
                            // new SbWaitForCut(door.Cuts, false),
                            new SbCutsceneBars(
                                new SbCut(door.Cut,
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

        private int GetMaxEnemiesToWalkIn(PlotBuilder builder)
        {
            var type = builder.EnemyType ?? Re2EnemyIds.ZombieRandom;
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
