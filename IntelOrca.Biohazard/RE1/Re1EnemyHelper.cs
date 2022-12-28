using System;
using System.Linq;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.RE1
{
    internal class Re1EnemyHelper : IEnemyHelper
    {
        private static readonly byte[] _zombieTypes = new byte[]
        {
            Re1EnemyIds.Zombie,
            Re1EnemyIds.ZombieNaked,
            Re1EnemyIds.ZombieResearcher
        };

        public string GetEnemyName(byte type)
        {
            var name = new Bio1ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public void GetEnemyProbabilities(RandoConfig config, Action<byte, double> addIfSupported)
        {
            switch (config.EnemyDifficulty)
            {
                case 0:
                    AddZombieTypes(20, addIfSupported);
                    addIfSupported(Re1EnemyIds.Cerberus, 10);
                    addIfSupported(Re1EnemyIds.SpiderBrown, 5);
                    addIfSupported(Re1EnemyIds.SpiderBlack, 5);
                    addIfSupported(Re1EnemyIds.Crow, 20);
                    addIfSupported(Re1EnemyIds.Hunter, 5);
                    addIfSupported(Re1EnemyIds.Bee, 10);
                    addIfSupported(Re1EnemyIds.Chimera, 5);
                    addIfSupported(Re1EnemyIds.Snake, 20);
                    addIfSupported(Re1EnemyIds.Tyrant1, 1);
                    addIfSupported(Re1EnemyIds.Yawn1, 1);
                    addIfSupported(Re1EnemyIds.Yawn2, 1);
                    break;
                case 1:
                    AddZombieTypes(25, addIfSupported);
                    addIfSupported(Re1EnemyIds.Cerberus, 15);
                    addIfSupported(Re1EnemyIds.SpiderBrown, 10);
                    addIfSupported(Re1EnemyIds.SpiderBlack, 10);
                    addIfSupported(Re1EnemyIds.Crow, 10);
                    addIfSupported(Re1EnemyIds.Hunter, 10);
                    addIfSupported(Re1EnemyIds.Bee, 5);
                    addIfSupported(Re1EnemyIds.Chimera, 10);
                    addIfSupported(Re1EnemyIds.Snake, 10);
                    addIfSupported(Re1EnemyIds.Tyrant1, 2);
                    addIfSupported(Re1EnemyIds.Yawn1, 2);
                    addIfSupported(Re1EnemyIds.Yawn2, 2);
                    break;
                case 2:
                    AddZombieTypes(30, addIfSupported);
                    addIfSupported(Re1EnemyIds.Cerberus, 20);
                    addIfSupported(Re1EnemyIds.SpiderBrown, 10);
                    addIfSupported(Re1EnemyIds.SpiderBlack, 10);
                    addIfSupported(Re1EnemyIds.Crow, 1);
                    addIfSupported(Re1EnemyIds.Hunter, 15);
                    addIfSupported(Re1EnemyIds.Bee, 1);
                    addIfSupported(Re1EnemyIds.Snake, 1);
                    addIfSupported(Re1EnemyIds.Chimera, 15);
                    addIfSupported(Re1EnemyIds.Tyrant1, 5);
                    addIfSupported(Re1EnemyIds.Yawn1, 5);
                    addIfSupported(Re1EnemyIds.Yawn2, 5);
                    break;
                case 3:
                default:
                    AddZombieTypes(20, addIfSupported);
                    addIfSupported(Re1EnemyIds.Cerberus, 40);
                    addIfSupported(Re1EnemyIds.SpiderBrown, 12);
                    addIfSupported(Re1EnemyIds.SpiderBlack, 12);
                    addIfSupported(Re1EnemyIds.Crow, 1);
                    addIfSupported(Re1EnemyIds.Hunter, 25);
                    addIfSupported(Re1EnemyIds.Bee, 1);
                    addIfSupported(Re1EnemyIds.Snake, 1);
                    addIfSupported(Re1EnemyIds.Chimera, 25);
                    addIfSupported(Re1EnemyIds.Tyrant1, 10);
                    addIfSupported(Re1EnemyIds.Yawn1, 10);
                    addIfSupported(Re1EnemyIds.Yawn2, 10);
                    break;
            }
        }

        private static void AddZombieTypes(double prob, Action<byte, double> addIfSupported)
        {
            var subProb = prob / _zombieTypes.Length;
            foreach (var zombieType in _zombieTypes)
            {
                addIfSupported(zombieType, subProb);
            }
        }

        public void ExcludeEnemies(RandoConfig config, Rdt rdt, string difficulty, Action<byte> exclude)
        {
            var types = rdt.Enemies
                .Select(x => x.Type)
                .Where(IsEnemy)
                .ToArray();

            if (types.Length == 0)
                return;

            var type = types[0];
            if (types.Length > 1 &&
                type != Re1EnemyIds.Yawn1 &&
                type != Re1EnemyIds.Yawn2 &&
                type != Re1EnemyIds.Tyrant1 &&
                type != Re1EnemyIds.Tyrant2)
            {
                exclude(Re1EnemyIds.Yawn1);
                exclude(Re1EnemyIds.Tyrant1);
                exclude(Re1EnemyIds.Yawn2);
                exclude(Re1EnemyIds.Tyrant2);
            }

            switch (type)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieNaked:
                case Re1EnemyIds.ZombieResearcher:
                case Re1EnemyIds.Hunter:
                case Re1EnemyIds.SpiderBrown:
                    exclude(Re1EnemyIds.SpiderBrown);
                    exclude(Re1EnemyIds.SpiderBlack);
                    exclude(Re1EnemyIds.Crow);
                    exclude(Re1EnemyIds.Chimera);
                    break;

                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.Crow:
                case Re1EnemyIds.Bee:
                case Re1EnemyIds.Snake:
                case Re1EnemyIds.Neptune:
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Plant42Vines:
                case Re1EnemyIds.Yawn2:
                    exclude(Re1EnemyIds.Zombie);
                    exclude(Re1EnemyIds.ZombieNaked);
                    exclude(Re1EnemyIds.ZombieResearcher);
                    exclude(Re1EnemyIds.Cerberus);
                    exclude(Re1EnemyIds.SpiderBrown);
                    exclude(Re1EnemyIds.SpiderBlack);
                    exclude(Re1EnemyIds.Crow);
                    exclude(Re1EnemyIds.Hunter);
                    exclude(Re1EnemyIds.Chimera);
                    break;
                case Re1EnemyIds.Chimera:
                    exclude(Re1EnemyIds.Zombie);
                    exclude(Re1EnemyIds.ZombieNaked);
                    exclude(Re1EnemyIds.ZombieResearcher);
                    exclude(Re1EnemyIds.Cerberus);
                    exclude(Re1EnemyIds.SpiderBrown);
                    exclude(Re1EnemyIds.SpiderBlack);
                    exclude(Re1EnemyIds.Crow);
                    exclude(Re1EnemyIds.Hunter);
                    break;
                case Re1EnemyIds.SpiderBlack:
                    break;
            }
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            switch (enemy.Type)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieNaked:
                case Re1EnemyIds.ZombieResearcher:
                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.SpiderBrown:
                case Re1EnemyIds.Crow:
                case Re1EnemyIds.Hunter:
                case Re1EnemyIds.Bee:
                case Re1EnemyIds.Chimera:
                case Re1EnemyIds.Snake:
                case Re1EnemyIds.Neptune:
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Plant42Vines:
                case Re1EnemyIds.Yawn2:
                    return true;
                default:
                    return false;
            }
        }

        public void BeginRoom(Rdt rdt)
        {
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyType)
        {
            switch (enemyType)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieNaked:
                case Re1EnemyIds.ZombieResearcher:
                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.SpiderBrown:
                case Re1EnemyIds.SpiderBlack:
                case Re1EnemyIds.Crow:
                case Re1EnemyIds.Hunter:
                case Re1EnemyIds.Bee:
                case Re1EnemyIds.Chimera:
                case Re1EnemyIds.Snake:
                case Re1EnemyIds.Neptune:
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Plant42Vines:
                    if (!enemySpec.KeepState)
                        enemy.State = 0;
                    break;
            }
        }

        public bool IsEnemy(byte type)
        {
            return type <= Re1EnemyIds.Yawn2;
        }
    }
}
