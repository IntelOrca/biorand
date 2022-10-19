using System;
using System.IO;
using rer;

class Program
{
    private static Random _random = new Random();

    public static void Main(string[] args)
    {
        var randomItems = new Random(2);
        var randomEnemies = new Random(2);
        var randomMusic = new Random(2);

        var re2Path = @"F:\games\re2";
        var originalDataPath = Path.Combine(re2Path, "data");
        var modPath = Path.Combine(re2Path, @"mod_rando");

        if (Directory.Exists(modPath))
        {
            Directory.Delete(modPath, true);
        }
        Directory.CreateDirectory(modPath);
        File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BIOHAZARD 2: RANDOMIZER\n");

        // var factory = new PlayGraphFactory(randomItems);
        var gameData = GameDataReader.Read(originalDataPath, modPath);
        CheckRoomItems(gameData);
        // factory.Create(gameData, @"M:\git\rer\rer\data\clairea.json");
        // factory.Save();

        RandomizeEnemies(gameData, randomEnemies);

        var bgmRandomiser = new BgmRandomiser(originalDataPath, modPath);
        bgmRandomiser.Randomise(randomMusic);
    }

    private static void RandomizeEnemies(GameData gameData, Random random)
    {
        foreach (var rdt in gameData.Rdts)
        {
            var enemyType = random.NextOf<byte>(23);
            foreach (var enemy in rdt.Enemies)
            {
                switch (enemyType)
                {
                    case 16: // zombie cop
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(0, 6),
                            ai: random.NextOf<byte>(0),
                            soundBank: random.NextOf<byte>(7, 8),
                            texture: random.NextOf<byte>(131));
                        break;
                    case 18: // zombie guy
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(70),
                            ai: random.NextOf<byte>(0),
                            soundBank: random.NextOf<byte>(3),
                            texture: 131);
                        break;
                    case 19: // zombie girl
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(0, 6, 67),
                            ai: random.NextOf<byte>(0),
                            soundBank: random.NextOf<byte>(10, 11),
                            texture: 0);
                        break;
                    case 21: // zombie lab coat (full)
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(0, 6),
                            ai: random.NextOf<byte>(0),
                            soundBank: random.NextOf<byte>(47),
                            texture: 128);
                        break;
                    case 22: // zombie lab coat (jacket)
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(0),
                            ai: random.NextOf<byte>(0),
                            soundBank: random.NextOf<byte>(47),
                            texture: 0);
                        break;
                    case 23: // zombie naked
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(0, 6),
                            ai: random.NextOf<byte>(0),
                            soundBank: random.NextOf<byte>(46),
                            texture: 0);
                        break;
                    case 31: // zombie guy
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: random.NextOf<byte>(70),
                            ai: random.NextOf<byte>(16),
                            soundBank: random.NextOf<byte>(1, 2),
                            texture: random.NextOf<byte>(0, 1, 2, 3));
                        break;
                    case 34: // licker
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: 0,
                            ai: 0,
                            soundBank: 14,
                            texture: 0);
                        break;
                    case 41: // bug
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: 0,
                            ai: 0,
                            soundBank: 15,
                            texture: 0);
                        break;
                    case 46: // plant
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: 0,
                            ai: 0,
                            soundBank: 19,
                            texture: 0);
                        break;
                    case 47: // ?
                        break;
                    case 57: // plant
                        rdt.SetEnemy(enemy.Id,
                            type: enemyType,
                            state: 16,
                            ai: 0,
                            soundBank: 19,
                            texture: 0);
                        break;
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
