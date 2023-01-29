using System;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2EnemyHelper : IEnemyHelper
    {
        private static readonly byte[] _zombieTypes = new byte[]
        {
            (byte)EnemyType.ZombieCop,
            (byte)EnemyType.ZombieGuy1,
            (byte)EnemyType.ZombieGirl,
            (byte)EnemyType.ZombieTestSubject,
            (byte)EnemyType.ZombieScientist,
            (byte)EnemyType.ZombieNaked,
            (byte)EnemyType.ZombieGuy2,
            (byte)EnemyType.ZombieGuy3,
            (byte)EnemyType.ZombieRandom,
            (byte)EnemyType.ZombieBrad
        };

        public string GetEnemyName(byte type)
        {
            return ((EnemyType)type).ToString();
        }

        public void GetEnemyProbabilities(RandoConfig config, Action<byte, double> addIfSupported)
        {
            switch (config.EnemyDifficulty)
            {
                case 0:
                    addIfSupported((byte)EnemyType.Crow, 10);
                    addIfSupported((byte)EnemyType.ZombieArms, 10);
                    addIfSupported((byte)EnemyType.Spider, 10);
                    addIfSupported((byte)EnemyType.GiantMoth, 2);
                    addIfSupported((byte)EnemyType.Ivy, 3);
                    addIfSupported((byte)EnemyType.IvyPurple, 2);
                    addIfSupported((byte)EnemyType.Tyrant1, 1);
                    AddZombieTypes(50, addIfSupported);
                    addIfSupported((byte)EnemyType.Cerebrus, 8);
                    addIfSupported((byte)EnemyType.LickerRed, 2);
                    addIfSupported((byte)EnemyType.LickerGrey, 2);
                    break;
                case 1:
                    addIfSupported((byte)EnemyType.Crow, 5);
                    addIfSupported((byte)EnemyType.ZombieArms, 5);
                    addIfSupported((byte)EnemyType.Spider, 6);
                    addIfSupported((byte)EnemyType.GiantMoth, 5);
                    addIfSupported((byte)EnemyType.Ivy, 6);
                    addIfSupported((byte)EnemyType.IvyPurple, 6);
                    addIfSupported((byte)EnemyType.Tyrant1, 2);
                    AddZombieTypes(40, addIfSupported);
                    addIfSupported((byte)EnemyType.Cerebrus, 10);
                    addIfSupported((byte)EnemyType.LickerRed, 10);
                    addIfSupported((byte)EnemyType.LickerGrey, 5);
                    break;
                case 2:
                    addIfSupported((byte)EnemyType.Spider, 7);
                    addIfSupported((byte)EnemyType.GiantMoth, 3);
                    addIfSupported((byte)EnemyType.Ivy, 6);
                    addIfSupported((byte)EnemyType.IvyPurple, 6);
                    addIfSupported((byte)EnemyType.Tyrant1, 3);
                    AddZombieTypes(25, addIfSupported);
                    addIfSupported((byte)EnemyType.Cerebrus, 25);
                    addIfSupported((byte)EnemyType.LickerRed, 15);
                    addIfSupported((byte)EnemyType.LickerGrey, 10);
                    addIfSupported((byte)EnemyType.Birkin1, 5);
                    break;
                case 3:
                default:
                    addIfSupported((byte)EnemyType.Spider, 5);
                    addIfSupported((byte)EnemyType.GiantMoth, 2);
                    addIfSupported((byte)EnemyType.Ivy, 3);
                    addIfSupported((byte)EnemyType.IvyPurple, 3);
                    addIfSupported((byte)EnemyType.Tyrant1, 5);
                    AddZombieTypes(17, addIfSupported);
                    addIfSupported((byte)EnemyType.Cerebrus, 40);
                    addIfSupported((byte)EnemyType.LickerRed, 5);
                    addIfSupported((byte)EnemyType.LickerGrey, 20);
                    addIfSupported((byte)EnemyType.Birkin1, 25);
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

            if (types.Length != 1)
            {
                exclude((byte)EnemyType.Birkin1);
            }

            if (difficulty == "medium" && config.EnemyDifficulty < 2)
            {
                exclude((byte)EnemyType.LickerRed);
                exclude((byte)EnemyType.LickerGrey);
                exclude((byte)EnemyType.Cerebrus);
                exclude((byte)EnemyType.Tyrant1);
            }
            else if (difficulty == "hard" && config.EnemyDifficulty < 3)
            {
                exclude((byte)EnemyType.LickerRed);
                exclude((byte)EnemyType.LickerGrey);
                exclude((byte)EnemyType.Cerebrus);
                exclude((byte)EnemyType.Tyrant1);
            }
        }

        public void BeginRoom(Rdt rdt)
        {
            // Mute dead zombies or vines, this ensures our random enemy type
            // will be heard
            foreach (var enemy in rdt.Enemies)
            {
                if ((EnemyType)enemy.Type == EnemyType.Vines || (IsZombie((EnemyType)enemy.Type) && enemy.State == 2))
                {
                    enemy.SoundBank = 0;
                }
            }
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            switch ((EnemyType)enemy.Type)
            {
                case EnemyType.Crow:
                case EnemyType.Spider:
                case EnemyType.GiantMoth:
                case EnemyType.LickerRed:
                case EnemyType.LickerGrey:
                case EnemyType.Cerebrus:
                case EnemyType.Ivy:
                case EnemyType.IvyPurple:
                case EnemyType.ZombieBrad:
                    return true;
                case EnemyType.MarvinBranagh:
                    // Edge case: Marvin is only a zombie in scenario B
                    return config.Scenario == 1;
                default:
                    return IsZombie((EnemyType)enemy.Type);
            }
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyTypeRaw)
        {
            var enemyType = (EnemyType)enemyTypeRaw;
            switch (enemyType)
            {
                case EnemyType.ZombieGuy1:
                case EnemyType.ZombieGuy2:
                case EnemyType.ZombieGuy3:
                case EnemyType.ZombieGirl:
                case EnemyType.ZombieCop:
                case EnemyType.ZombieTestSubject:
                case EnemyType.ZombieScientist:
                case EnemyType.ZombieNaked:
                case EnemyType.ZombieRandom:
                case EnemyType.ZombieBrad:
                    if (!enemySpec.KeepState)
                        enemy.State = rng.NextOf<byte>(0, 1, 2, 3, 4, 6);
                    enemy.SoundBank = GetZombieSoundBank(enemyType);
                    break;
                case EnemyType.Cerebrus:
                    enemy.State = 0;
                    if (config.EnemyDifficulty >= 3)
                    {
                        // %50 of running
                        enemy.State = rng.NextOf<byte>(0, 2);
                    }
                    else if (config.EnemyDifficulty >= 2)
                    {
                        // %25 of running
                        enemy.State = rng.NextOf<byte>(0, 0, 0, 2);
                    }
                    enemy.SoundBank = 12;
                    break;
                case EnemyType.ZombieArms:
                    enemy.State = 0;
                    enemy.SoundBank = 17;
                    break;
                case EnemyType.Crow:
                    enemy.State = 0;
                    enemy.SoundBank = 13;
                    break;
                case EnemyType.BabySpider:
                case EnemyType.Spider:
                    enemy.State = 0;
                    enemy.SoundBank = 16;
                    break;
                case EnemyType.LickerRed:
                case EnemyType.LickerGrey:
                    enemy.State = 0;
                    enemy.SoundBank = 14;
                    break;
                case EnemyType.Cockroach:
                    enemy.State = 0;
                    enemy.SoundBank = 15;
                    break;
                case EnemyType.Ivy:
                case EnemyType.IvyPurple:
                    enemy.State = 0;
                    enemy.SoundBank = 19;
                    break;
                case EnemyType.GiantMoth:
                    enemy.State = 0;
                    enemy.SoundBank = 23;
                    break;
                case EnemyType.Tyrant1:
                    enemy.State = 0;
                    enemy.SoundBank = 18;
                    break;
                case EnemyType.Birkin1:
                    enemy.State = 1;
                    enemy.SoundBank = 24;
                    break;
            }
        }

        private static byte GetZombieSoundBank(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.ZombieCop:
                case EnemyType.ZombieGuy1:
                case EnemyType.ZombieGuy2:
                case EnemyType.ZombieGuy3:
                case EnemyType.ZombieRandom:
                case EnemyType.ZombieScientist:
                case EnemyType.ZombieTestSubject:
                case EnemyType.ZombieBrad:
                    return 1;
                case EnemyType.ZombieGirl:
                    return 10;
                case EnemyType.ZombieNaked:
                    return 46;
                default:
                    return 0;
            }
        }

        private static bool IsZombie(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.ZombieCop:
                case EnemyType.ZombieBrad:
                case EnemyType.ZombieGuy1:
                case EnemyType.ZombieGirl:
                case EnemyType.ZombieTestSubject:
                case EnemyType.ZombieScientist:
                case EnemyType.ZombieNaked:
                case EnemyType.ZombieGuy2:
                case EnemyType.ZombieGuy3:
                case EnemyType.ZombieRandom:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsEnemy(byte type)
        {
            return type < (byte)EnemyType.ChiefIrons1;
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Arms", "LightGray", new[] { (byte)EnemyType.ZombieArms }),
            new SelectableEnemy("Crow", "Black", new[] { (byte)EnemyType.Crow }),
            new SelectableEnemy("Spider", "YellowGreen", new[] { (byte)EnemyType.Spider }),
            new SelectableEnemy("Zombie", "LightGray", new[] { (byte)EnemyType.ZombieRandom }),
            new SelectableEnemy("Moth", "DarkOliveGreen", new[] { (byte)EnemyType.GiantMoth }),
            new SelectableEnemy("Ivy", "SpringGreen", new[] { (byte)EnemyType.Ivy }),
            new SelectableEnemy("Licker", "IndianRed", new[] { (byte)EnemyType.LickerRed, (byte)EnemyType.LickerGrey }),
            new SelectableEnemy("Cerberus", "Black", new[] { (byte)EnemyType.Cerebrus }),
            new SelectableEnemy("Tyrant", "DarkGray", new[] { (byte)EnemyType.Tyrant1 }),
            new SelectableEnemy("Birkin", "IndianRed", new[] { (byte)EnemyType.Birkin1 }),
        };
    }
}
