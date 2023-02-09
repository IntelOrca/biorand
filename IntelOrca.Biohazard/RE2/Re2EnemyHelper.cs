using System;
using System.Collections.Generic;
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

        public bool SupportsEnemyType(RandoConfig config, Rdt rdt, string difficulty, bool hasEnemyPlacements, byte enemyType)
        {
            if (config.RandomEnemyPlacement && hasEnemyPlacements)
            {
                return true;
            }
            else
            {
                var exclude = new HashSet<byte>();
                ExcludeEnemies(config, rdt, difficulty, x => exclude.Add(x));
                return !exclude.Contains(enemyType);
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

        public bool IsUniqueEnemyType(byte type)
        {
            switch ((EnemyType)type)
            {
                case EnemyType.Alligator:
                case EnemyType.Tyrant1:
                case EnemyType.Tyrant2:
                case EnemyType.Birkin1:
                case EnemyType.Birkin2:
                case EnemyType.Birkin3:
                case EnemyType.Birkin4:
                case EnemyType.Birkin5:
                    return true;
                default:
                    return false;
            }
        }

        public int GetEnemyTypeLimit(RandoConfig config, byte type)
        {
            switch ((EnemyType)type)
            {
                case EnemyType.Birkin1:
                    return 1;
                case EnemyType.Cerebrus:
                case EnemyType.GiantMoth:
                case EnemyType.Ivy:
                case EnemyType.IvyPurple:
                    return 8;
                case EnemyType.LickerRed:
                case EnemyType.LickerGrey:
                case EnemyType.Tyrant1:
                    return 5;
                default:
                    return 16;
            }
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Arms", "LightGray", new[] { (byte)EnemyType.ZombieArms }),
            new SelectableEnemy("Crow", "Black", new[] { (byte)EnemyType.Crow }),
            new SelectableEnemy("Spider", "YellowGreen", new[] { (byte)EnemyType.Spider }),
            new SelectableEnemy("Zombie", "LightGray", _zombieTypes),
            new SelectableEnemy("Moth", "DarkOliveGreen", new[] { (byte)EnemyType.GiantMoth }),
            new SelectableEnemy("Ivy", "SpringGreen", new[] { (byte)EnemyType.Ivy }),
            new SelectableEnemy("Licker", "IndianRed", new[] { (byte)EnemyType.LickerRed, (byte)EnemyType.LickerGrey }),
            new SelectableEnemy("Cerberus", "Black", new[] { (byte)EnemyType.Cerebrus }),
            new SelectableEnemy("Tyrant", "DarkGray", new[] { (byte)EnemyType.Tyrant1 }),
            new SelectableEnemy("Birkin", "IndianRed", new[] { (byte)EnemyType.Birkin1 }),
        };
    }
}
