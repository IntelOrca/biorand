using System;
using System.Linq;

namespace rer
{
    internal class EnemyRandomiser
    {
        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Random _random;

        public EnemyRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Random random)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _random = random;
        }

        public void Randomise()
        {
            _logger.WriteHeading("Randomizing enemies:");
            foreach (var rdt in _gameData.Rdts)
            {
                var logEnemies = rdt.Enemies.Select(GetEnemyLogText).ToArray();
                RandomiseRoom(rdt);
                rdt.Save();
                for (int i = 0; i < logEnemies.Length; i++)
                {
                    var enemy = rdt.Enemies[i];
                    var oldLog = logEnemies[i];
                    var newLog = GetEnemyLogText(enemy);
                    if (oldLog != newLog)
                    {
                        _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) {oldLog} becomes {newLog}");
                    }
                }
            }
        }

        private static string GetEnemyLogText(RdtEnemy enemy)
        {
            return $"[{enemy.Type},{enemy.State},{enemy.Ai},{enemy.SoundBank},{enemy.Texture}]";
        }

        private void RandomiseRoom(Rdt rdt)
        {
            if (rdt.ToString() == "101")
                return;
            if (rdt.ToString() == "212")
                return;

            if (rdt.ToString() == "100")
                rdt.Nop(4888, 4);

            if (rdt.ToString() == "105")
            {
                var enemyType3 = GetRandomZombieType();
                foreach (var enemy in rdt.Enemies)
                {
                    enemy.Type = enemyType3;
                    enemy.SoundBank = GetZombieSoundBank(enemy.Type);
                }
                return;
            }

            if (rdt.Enemies.Any(x => x.State == 72))
            {
                var enemyType3 = _random.NextOf(EnemyType.ZombieRandom, EnemyType.ZombieNaked);
                foreach (var enemy in rdt.Enemies)
                {
                    enemy.Type = enemyType3;
                    enemy.SoundBank = GetZombieSoundBank(enemy.Type);
                }
                return;
            }

#if MULTIPLE_ENEMY_TYPES
            var ids = rdt.Enemies.Select(x => x.Id).Distinct().ToArray();
            var enemyTypesId = ids.Select(x => EnemyType.Cerebrus).ToArray();
            if (enemyTypesId.Length >= 2)
                enemyTypesId[0] = EnemyType.LickerRed;
            if (enemyTypesId.Length >= 3)
                enemyTypesId[1] = EnemyType.LickerRed;
#else
            var ids = rdt.Enemies.Select(x => x.Id).Distinct().ToArray();
            var randomEnemyType = GetRandomEnemyType();
            var enemyTypesId = ids.Select(x => randomEnemyType).ToArray();
#endif

            foreach (var enemy in rdt.Enemies)
            {
                if (ShouldChangeEnemy(rdt, enemy))
                {
                    var index = Array.IndexOf(ids, enemy.Id);
                    var enemyType = enemyTypesId[index];
                    enemy.Type = enemyType;
                    enemy.State = 0;
                    enemy.Ai = 0;
                    enemy.Texture = 0;
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
                            enemy.State = _random.NextOf<byte>(0, 1, 2, 3, 4, 6);
                            enemy.SoundBank = GetZombieSoundBank(enemyType);
                            break;
                        case EnemyType.Cerebrus:
                            enemy.State = _random.NextOf<byte>(0, 0, 0, 2);
                            enemy.Ai = 0;
                            enemy.SoundBank = 12;
                            break;
                        case EnemyType.ZombieArms:
                            enemy.SoundBank = 17;
                            break;
                        case EnemyType.Crow:
                            enemy.SoundBank = 13;
                            break;
                        case EnemyType.BabySpider:
                        case EnemyType.Spider:
                            enemy.SoundBank = 16;
                            break;
                        case EnemyType.LickerRed:
                        case EnemyType.LickerGrey:
                            enemy.SoundBank = 14;
                            break;
                        case EnemyType.Cockroach:
                            enemy.SoundBank = 15;
                            break;
                        case EnemyType.Ivy:
                        case EnemyType.IvyPurple:
                            enemy.SoundBank = 19;
                            break;
                        case EnemyType.GiantMoth:
                            enemy.SoundBank = 23;
                            break;
                        case EnemyType.Tyrant1:
                            enemy.SoundBank = 18;
                            break;
                    }
                }
            }
        }

        private byte GetZombieSoundBank(EnemyType type)
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
                    return _random.NextOf<byte>(1, 2);
                case EnemyType.ZombieGirl:
                    return 10;
                case EnemyType.ZombieNaked:
                    return 46;
                default:
                    return 0;
            }
        }

        private bool ShouldChangeEnemy(Rdt rdt, RdtEnemy enemy)
        {
            if (enemy.Type == EnemyType.ZombieBrad)
                return false;

            if (enemy.Type == EnemyType.LickerRed ||
                enemy.Type == EnemyType.LickerGrey)
            {
                if (enemy.State == 0)
                    return true;
                return false;
            }

            if (IsZombie(enemy.Type))
            {
                // Dead zombie (e.g. first licker corridor)
                if (enemy.State == 7)
                    return false;

                return true;
            }

            if (enemy.Type == EnemyType.Cerebrus || enemy.Type == EnemyType.Ivy || enemy.Type == EnemyType.IvyPurple)
                return true;

            return false;
        }

        private EnemyType GetRandomEnemyType()
        {
            return _random.NextOf(
               EnemyType.ZombieCop,
               EnemyType.ZombieGuy1,
               EnemyType.ZombieGirl,
               EnemyType.ZombieTestSubject,
               EnemyType.ZombieScientist,
               EnemyType.ZombieNaked,
               EnemyType.ZombieGuy2,
               EnemyType.ZombieGuy3,
               EnemyType.ZombieRandom,
               EnemyType.ZombieArms,
               EnemyType.Cerebrus,
               EnemyType.Crow,
               EnemyType.BabySpider,
               EnemyType.Spider,
               EnemyType.LickerRed,
               EnemyType.LickerGrey,
               EnemyType.Ivy,
               EnemyType.IvyPurple,
               EnemyType.GiantMoth,
               EnemyType.Tyrant1);
        }

        private EnemyType GetRandomZombieType()
        {
            return _random.NextOf(
                EnemyType.ZombieCop,
                EnemyType.ZombieGuy1,
                EnemyType.ZombieGirl,
                EnemyType.ZombieTestSubject,
                EnemyType.ZombieScientist,
                EnemyType.ZombieNaked,
                EnemyType.ZombieGuy2,
                EnemyType.ZombieGuy3,
                EnemyType.ZombieRandom);
        }

        private static void PrintAllEnemies(GameData gameData)
        {
            foreach (var rdt in gameData.Rdts)
            {
                if (rdt.Enemies.Count != 0)
                {
                    Console.WriteLine($"RDT: {rdt.RdtId}:");
                    foreach (var enemy in rdt.Enemies)
                    {
                        Console.WriteLine($"    {enemy.Id}: {enemy.Type}, {enemy.State}, {enemy.Ai}, {enemy.SoundBank}, {enemy.Texture}");
                    }
                }
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
    }
}
