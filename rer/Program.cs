using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace rer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var rng = new Rng();

            var numbers = new List<double>();
            for (int i = 0; i < 2048; i++)
                // numbers.Add((5 + rng.NextGaussian(-5, 1)) / 10);
                numbers.Add(rng.NextGaussian(-5, 2));
            numbers.Sort();

            // Generate(new RandoConfig()
            // {
            //     Seed = 0
            // });
        }

        public static void Generate(RandoConfig config)
        {
            var randomItems = new Rng(config.Seed);
            var randomNpcs = new Rng(config.Seed + 1);
            var randomEnemies = new Rng(config.Seed + 2);
            var randomMusic = new Rng(config.Seed + 3);

            var re2Path = @"F:\games\re2";
            var originalDataPath = Path.Combine(re2Path, "data");
            var modPath = Path.Combine(re2Path, @"mod_rando");

            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, true);
            }
            Directory.CreateDirectory(modPath);

            using var logger = new RandoLogger(Path.Combine(modPath, "log.txt"));
            logger.WriteHeading("Resident Evil Randomizer");
            logger.WriteLine($"Seed: {config}");

            var map = LoadJsonMap(@"M:\git\rer\rer\data\clairea.json");

            var gameData = GameDataReader.Read(originalDataPath, modPath);

            if (config.RandomItems)
            {
                var factory = new PlayGraphFactory(logger, config, gameData, map, randomItems);
                // CheckRoomItems(gameData);
                // factory.CreateDoorRando();
                factory.Create();
                factory.Save();
            }

            if (config.RandomNPCs)
            {
                var npcRandomiser = new NPCRandomiser(logger, originalDataPath, modPath, gameData, map, randomNpcs);
                npcRandomiser.Randomise();
            }

            if (config.RandomEnemies)
            {
                var enemyRandomiser = new EnemyRandomiser(logger, config, gameData, randomEnemies);
                enemyRandomiser.Randomise();
            }

            if (config.RandomBgm)
            {
                var bgmRandomiser = new BgmRandomiser(logger, originalDataPath, modPath);
                bgmRandomiser.Randomise(randomMusic);
            }

            File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BIOHAZARD 2: RANDOMIZER\n");
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
    }
}
