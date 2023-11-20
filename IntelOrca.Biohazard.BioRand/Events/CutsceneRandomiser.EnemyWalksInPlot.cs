using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class EnemyWalksInPlot : Plot
        {
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
