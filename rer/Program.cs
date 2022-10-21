using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using rer;

class Program
{
    public static void Main(string[] args)
    {
        var randomItems = new Random(3);
        var randomNpcs = new Random(4);
        var randomEnemies = new Random(4);
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

        var map = LoadJsonMap(@"M:\git\rer\rer\data\clairea.json");

        var factory = new PlayGraphFactory(randomItems);
        var gameData = GameDataReader.Read(originalDataPath, modPath);
        // CheckRoomItems(gameData);
        // factory.CreateDoorRando(gameData, map);
        factory.Create(gameData, map);
        factory.Save();

        var npcRandomiser = new NPCRandomiser(originalDataPath, modPath, gameData, map, randomNpcs);
        npcRandomiser.Randomise();

        var enemyRandomiser = new EnemyRandomiser(gameData, randomEnemies);
        enemyRandomiser.Randomise();

        var bgmRandomiser = new BgmRandomiser(originalDataPath, modPath);
        bgmRandomiser.Randomise(randomMusic);
    }

    private static Map LoadJsonMap(string path)
    {
        var jsonMap = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;
        return map;
    }

    private static void RandomizeNpcs(GameData gameData, Random random)
    {
        Console.WriteLine($"Randomising NPCs");
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
                    Console.WriteLine($"    {rdt.RdtId}:{enemy.Id} [{enemy.Type}] becomes [{newType}]");
                    enemy.Type = EnemyType.SherryWithClairesJacket;
                }
            }
            rdt.Save();
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
