using System;
using System.IO;
using System.Linq;
using rer;

class Program
{
    private static Random _random = new Random();

    public static void Main(string[] args)
    {
        var randomItems = new Random(3);
        var randomNpcs = new Random(3);
        var randomEnemies = new Random(3);
        var randomMusic = new Random(3);

        var re2Path = @"F:\games\re2";
        var originalDataPath = Path.Combine(re2Path, "data");
        var modPath = Path.Combine(re2Path, @"mod_rando");

        if (Directory.Exists(modPath))
        {
            Directory.Delete(modPath, true);
        }
        Directory.CreateDirectory(modPath);
        File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BIOHAZARD 2: RANDOMIZER\n");

        var factory = new PlayGraphFactory(randomItems);
        var gameData = GameDataReader.Read(originalDataPath, modPath);
        // CheckRoomItems(gameData);
        factory.CreateDoorRando(gameData, @"M:\git\rer\rer\data\clairea.json");
        // factory.Create(gameData, @"M:\git\rer\rer\data\clairea.json");
        factory.Save();

        // RandomizeNpcs(gameData, randomEnemies);
        // RandomizeEnemies(gameData, randomEnemies);

        var bgmRandomiser = new BgmRandomiser(originalDataPath, modPath);
        bgmRandomiser.Randomise(randomMusic);
    }

    private static void RandomizeNpcs(GameData gameData, Random random)
    {
        var sourceNpc = new[]
        {
            EnemyType.ChiefIrons1,
            EnemyType.AdaWong1,
            EnemyType.ChiefIrons2,
            EnemyType.AdaWong2,
            EnemyType.BenBertolucci1,
            EnemyType.SherryWithPendant,
            EnemyType.BenBertolucci2,
            EnemyType.AnnetteBirkin1,
            EnemyType.RobertKendo,
            EnemyType.AnnetteBirkin2,
            EnemyType.MarvinBranagh,
            EnemyType.SherryWithClairesJacket,
            EnemyType.LeonKennedyRpd,
            EnemyType.ClaireRedfield,
            EnemyType.LeonKennedyBandaged,
            EnemyType.ClaireRedfieldNoJacket,
            EnemyType.LeonKennedyCapTankTop,
            EnemyType.ClaireRedfieldCowGirl,
            EnemyType.LeonKennedyBlackLeather,
        };
        var targetNpc = sourceNpc.Shuffle(random).ToArray();
        var map = Enumerable.Range(0, sourceNpc.Length).ToDictionary(x => sourceNpc[x], i => targetNpc[i]);

        foreach (var rdt in gameData.Rdts)
        {
            foreach (var enemy in rdt.Enemies)
            {
                if (map.TryGetValue(enemy.Type, out var newType))
                {
                    enemy.Type = EnemyType.SherryWithClairesJacket;
                }
            }
            rdt.Save();
        }
    }

    private static void RandomizeEnemies(GameData gameData, Random random)
    {
        foreach (var rdt in gameData.Rdts)
        {
            var enemyType = random.NextOf(
                EnemyType.ZombieCop,
                EnemyType.ZombieGuy1,
                EnemyType.ZombieGirl,
                EnemyType.ZombieTestSubject,
                EnemyType.ZombieScientist,
                EnemyType.ZombieNaked,
                EnemyType.ZombieGuy2,
                EnemyType.ZombieGuy3,
                EnemyType.ZombieRandom,
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
            foreach (var enemy in rdt.Enemies)
            {
                if ((IsZombie(enemy.Type) && enemy.Type != EnemyType.ZombieBrad) || enemy.Type == EnemyType.Cerebrus || enemy.Type == EnemyType.Ivy || enemy.Type == EnemyType.IvyPurple)
                {
                    switch (enemyType)
                    {
                        case EnemyType.ZombieCop:
                        case EnemyType.ZombieGuy1:
                        case EnemyType.ZombieGirl:
                        case EnemyType.ZombieTestSubject:
                        case EnemyType.ZombieScientist:
                        case EnemyType.ZombieNaked:
                        case EnemyType.ZombieGuy2:
                        case EnemyType.ZombieGuy3:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: random.NextOf<byte>(0, 1, 2, 3, 4, 6),
                                ai: 0,
                                soundBank: random.NextOf<byte>(1, 2, 7, 8, 41, 46, 47),
                                texture: 0);
                            break;
                        case EnemyType.Cerebrus:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: random.NextOf<byte>(0, 2),
                                ai: 0,
                                soundBank: 12,
                                texture: 0);
                            break;
                        case EnemyType.Crow:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 13,
                                texture: 0);
                            break;
                        case EnemyType.BabySpider:
                        case EnemyType.Spider:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 16,
                                texture: 0);
                            break;
                        case EnemyType.LickerRed:
                        case EnemyType.LickerGrey:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 14,
                                texture: 0);
                            break;
                        case EnemyType.Cockroach:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 15,
                                texture: 0);
                            break;
                        case EnemyType.Ivy:
                        case EnemyType.IvyPurple:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 19,
                                texture: 0);
                            break;
                        case EnemyType.GiantMoth:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 23,
                                texture: 0);
                            break;
                        case EnemyType.Tyrant1:
                            rdt.SetEnemy(enemy.Id,
                                type: enemyType,
                                state: 0,
                                ai: 0,
                                soundBank: 18,
                                texture: 0);
                            break;
                    }
                }
            }
            rdt.Save();
        }
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

    private static bool IsCreature(EnemyType type) => type < EnemyType.ChiefIrons1;

    private static bool IsZombie(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.ZombieCop:
            case EnemyType.ZombieBrad:
            case EnemyType.ZombieGuy1:
            case EnemyType.ZombieGirl:
            case EnemyType.ZombieTestSubject :
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

    private static void CheckRoomItems(GameData gameData)
    {
        var rtd = gameData.GetRdt(RdtId.Parse("100"))!;
        // rtd.SetItem(1, 0x43, 1); // crank
        // rtd.SetItem(3, 0x33, 1); // red jewel
        // rtd.SetItem(7, 0x2F, 1); // lighter
        // rtd.SetItem(6, 0x0D, 1); // colt
        // rtd.SetItem(7, 0x60, 1); // mo disk
        // rtd.SetItem(8, 0x61, 1); // umbrella card
        foreach (var rdt in gameData.Rdts)
        {
            rdt.Save();
        }
    }
}
