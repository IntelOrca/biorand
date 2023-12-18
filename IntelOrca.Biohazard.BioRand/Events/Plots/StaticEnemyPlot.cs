using System;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class StaticEnemyPlot : IPlot
    {
        private const byte POSE_DOG_IDLE = 0;
        private const byte POSE_DOG_HOSTILE = 2;
        private const byte POSE_DOG_EATING = 5;

        private const byte POSE_ZOMBIE_WAIT = 0x00;
        private const byte POSE_ZOMBIE_LYING = 0x01;
        private const byte POSE_ZOMBIE_WAKE_UP = 0x02;
        private const byte POSE_ZOMBIE_CRAWL = 0x03;
        private const byte POSE_ZOMBIE_GET_UP = 0x04;
        private const byte POSE_ZOMBIE_DEAD_UP = 0x05;
        private const byte POSE_ZOMBIE_FOLLOW = 0x06;
        private const byte POSE_ZOMBIE_DEAD = 0x07;
        private const byte POSE_ZOMBIE_EATING = 0x08;
        private const byte POSE_ZOMBIE_40 = 0x40;

        private const byte AI_IGNORE = 0x40;

        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var enemyType = builder.EnemyType ?? 0;
            var isZombie = builder.EnemyHelper.IsZombie(enemyType);
            var canLunch = isZombie || enemyType == Re2EnemyIds.ZombieDog;

            SbNode result;
            if (canLunch && builder.Rng.NextProbability(25))
            {
                result = CreateLunch(builder);
            }
            else
            {
                result = CreateSimple(builder);
            }
            return new CsPlot(new SbProcedure(result), endOfScript: true);
        }

        private SbNode CreateSimple(PlotBuilder builder)
        {
            var enemies = builder.AllocateEnemies();
            return new SbCommentNode($"[plot] {enemies.Length} enemies",
                builder.CreateEnemyConditionGuard(
                    enemies.Select(x => new SbEnemy(x)).ToArray()));
        }

        private SbNode CreateLunch(PlotBuilder builder)
        {
            var plotName = builder.EnemyType == Re2EnemyIds.ZombieDog ?
                "[plot] dog lunch" :
                "[plot] zombie lunch";

            var plotFlag = builder.AllocateGlobalFlag();

            var body = builder.AllocateEnemy(Re2EnemyIds.ZombieRandom, hasGlobalId: false);
            var bodyPose = builder.Rng.NextOf(POSE_ZOMBIE_DEAD, POSE_ZOMBIE_DEAD_UP);

            var enemies = builder.AllocateEnemies(max: 5);

            var nodeBuilder = new SbNodeBuilder();
            foreach (var enemy in enemies)
            {
                var radius = 1000;
                var pos = body.DefaultPosition.WithD(builder.Rng.Next(0, 4096));
                var a = pos.D * Math.PI / 2048;
                var dx = (int)(Math.Cos(a) * -radius);
                var dz = (int)(Math.Sin(a) * radius);
                var newPos = new REPosition(pos.X + dx, pos.Y, pos.Z + dz, pos.D);
                SbEnemy sbEnemy;
                if (builder.EnemyType == Re2EnemyIds.ZombieDog)
                {
                    sbEnemy = new SbEnemy(enemy, newPos, pose: POSE_DOG_EATING);
                }
                else
                {
                    sbEnemy = new SbEnemy(enemy, newPos, pose: POSE_ZOMBIE_EATING, ai: AI_IGNORE);
                }
                nodeBuilder.Append(sbEnemy);
            }
            var eatingEnemies = nodeBuilder.Build();

            foreach (var enemy in enemies)
            {
                nodeBuilder.Append(new SbEnemy(enemy));
            }
            var idleEnemies = nodeBuilder.Build();

            nodeBuilder.Append(new SbEnemy(body, enabled: false, pose: bodyPose, sound: 0));
            nodeBuilder.Append(new SbSetEntityCollision(body, false));
            nodeBuilder.Append(eatingEnemies);
            nodeBuilder.Append(
                new SbIf(plotFlag, false,
                    new SbSetFlag(plotFlag),
                    eatingEnemies)
                .Else(
                    idleEnemies));

            nodeBuilder.Reparent(x => builder.CreateEnemyConditionGuard(x));
            nodeBuilder.Reparent(x => new SbCommentNode(plotName, x));
            return nodeBuilder.Build();
        }
    }
}
