using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard
{
    public class Program
    {
#if !DEBUG
        private static Map? g_map;
#endif

#if DEBUG
        public static void Main(string[] args)
        {
            var re2Path = @"F:\games\re2";
            var originalDataPath = Path.Combine(re2Path, "data");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = re2Path;
            }

            var modPath = Path.Combine(re2Path, @"mod_biorand");

            var gameData = GameDataReader.Read(originalDataPath, modPath, 0);
            DumpRdtList(gameData, @"M:\git\rer\docs\rdt_leon.txt");
            DumpScripts(gameData, @"F:\games\re2\mod_biorand\scripts");

            gameData = GameDataReader.Read(originalDataPath, modPath, 1);
            DumpRdtList(gameData, @"M:\git\rer\docs\rdt_claire.txt");
        }
#endif

        private static string GetRe2DataPath(string re2Path)
        {
            var originalDataPath = Path.Combine(re2Path, "data");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = re2Path;
            }
            return originalDataPath;
        }

        public static int DoIntegrityCheck(string re2Path)
        {
            var allExpectedChecksums = JsonSerializer.Deserialize<Dictionary<string, ulong>[]>(Resources.checksum)!;

            var result = 0;
            var dataPath = GetRe2DataPath(re2Path);
            for (int player = 0; player < 2; player++)
            {
                var actualChecksums = GameDataReader.GetRdtChecksums(dataPath, player);
                var expectedChecksums = allExpectedChecksums[player];
                foreach (var kvp in expectedChecksums)
                {
                    if (actualChecksums.TryGetValue(RdtId.Parse(kvp.Key), out var actualChecksum))
                    {
                        if (kvp.Value != actualChecksum)
                        {
                            result = 1;
                        }
                    }
                    else
                    {
                        return 2;
                    }
                }
            }
            return result;
        }

        public static void Generate(RandoConfig config, string re2Path)
        {
            if (config.Version < RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with an older version of the randomizer and can not be played.");
            }
            else if (config.Version != RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with a newer version of the randomizer and can not be played.");
            }

            var originalDataPath = GetRe2DataPath(re2Path);
            var modPath = Path.Combine(re2Path, @"mod_biorand");

            if (Directory.Exists(modPath))
            {
                try
                {
                    Directory.Delete(modPath, true);
                }
                catch
                {
                }
            }
            Directory.CreateDirectory(modPath);

            var po = new ParallelOptions();
#if DEBUG
            po.MaxDegreeOfParallelism = 1;
#endif
            if (config.GameVariant == 0)
            {
                // Leon A / Claire B
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 0), originalDataPath, modPath),
                    () => GenerateRdts(config.WithPlayerScenario(1, 1), originalDataPath, modPath));
            }
            else
            {
                // Leon B / Claire A
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 1), originalDataPath, modPath),
                    () => GenerateRdts(config.WithPlayerScenario(1, 0), originalDataPath, modPath));
            }

            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading("Resident Evil Randomizer");
                logger.WriteLine($"Seed: {config}");

                var bgmRandomiser = new BgmRandomiser(logger, originalDataPath, modPath);
                bgmRandomiser.Randomise(new Rng(config.Seed));
            }

            File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BioRand: A Resident Evil Randomizer\n");
        }

        public static void GenerateRdts(RandoConfig config, string originalDataPath, string modPath)
        {
            var baseSeed = config.Seed + config.Player;
            var randomDoors = new Rng(baseSeed + 1);
            var randomItems = new Rng(baseSeed + 2);
            var randomEnemies = new Rng(baseSeed + 3);
            var randomNpcs = new Rng(baseSeed + 4);

            using var logger = new RandoLogger(Path.Combine(modPath, $"log_pl{config.Player}.txt"));
            logger.WriteHeading("Resident Evil Randomizer");
            logger.WriteLine($"Seed: {config}");
            logger.WriteLine($"Player: {config.Player} {GetPlayerName(config.Player)}");
            logger.WriteLine($"Scenario: {GetScenarioName(config.Scenario)}");

            try
            {
                var map = GetJsonMap();
                var gameData = GameDataReader.Read(originalDataPath, modPath, config.Player);

                if (config.RandomDoors || config.RandomItems)
                {
                    var dgmlPath = Path.Combine(modPath, $"graph_pl{config.Player}.dgml");
                    var doorRando = new DoorRandomiser(logger, config, gameData, map, randomDoors);
                    var itemRando = new ItemRandomiser(logger, config, gameData, map, randomItems);
                    var graph = config.RandomDoors ?
                        doorRando.CreateRandomGraph() :
                        doorRando.CreateOriginalGraph();
                    try
                    {
                        itemRando.RandomiseItems(graph);
                    }
                    finally
                    {
                        graph.GenerateDgml(dgmlPath);
                    }
                }

                if (config.RandomEnemies)
                {
                    var enemyRandomiser = new EnemyRandomiser(logger, config, gameData, map, randomEnemies);
                    enemyRandomiser.Randomise();
                }

                if (config.RandomNPCs)
                {
                    var npcRandomiser = new NPCRandomiser(logger, config, originalDataPath, modPath, gameData, map, randomNpcs);
                    npcRandomiser.Randomise();
                }

                foreach (var rdt in gameData.Rdts)
                {
                    rdt.Save();
                }

#if DEBUG
                if (config.RandomItems || config.RandomEnemies)
                {
                    DumpScripts(gameData, Path.Combine(modPath, $"scripts_pl{config.Player}"));
                    var moddedGameData = GameDataReader.Read(modPath, modPath, config.Player);
                    DumpScripts(moddedGameData, Path.Combine(modPath, $"scripts_modded_pl{config.Player}"));
                }
#endif
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.Message);
                throw;
            }
        }

        private static string GetPlayerName(int player) => player == 0 ? "Leon" : "Claire";
        private static string GetScenarioName(int scenario) => scenario == 0 ? "A" : "B";

        private static Map GetJsonMap()
        {
#if DEBUG
            return LoadJsonMap();
#else
            if (g_map == null)
            {
                g_map = LoadJsonMap();
            }
            return g_map;
#endif
        }

        private static Map LoadJsonMap()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\rdt.json");
            var jsonMap = File.ReadAllText(jsonPath);
#else
            var jsonMap = Resources.rdt;
#endif
            var map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
            return map;
        }

        private static void DumpRdtList(GameData gameData, string path)
        {
            var sb = new StringBuilder();
            foreach (var rdt in gameData.Rdts)
            {
                sb.AppendLine($"{rdt.RdtId} ():");
                AstPrinter.Print(sb, rdt.Ast!);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void DumpRdt(StringBuilder sb, Rdt rdt)
        {
            foreach (var door in rdt.Doors)
            {
                sb.AppendLine($"    Door #{door.Id}: {new RdtId(door.NextStage, door.NextRoom)} (0x{door.Offset:X2})");
            }
            foreach (var item in rdt.Items)
            {
                sb.AppendLine($"    Item #{item.Id}: {(ItemType)item.Type} x{item.Amount} (0x{item.Offset:X2})");
            }
            foreach (var enemy in rdt.Enemies)
            {
                sb.AppendLine($"    Enemy #{enemy.Id}: {enemy.Type} (0x{enemy.Offset:X2})");
            }
        }

        private static void DumpScripts(GameData gameData, string scriptPath)
        {
            Directory.CreateDirectory(scriptPath);
            foreach (var rdt in gameData.Rdts)
            {
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.bio"), rdt.Script);
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.s"), rdt.ScriptDisassembly);
            }
        }
    }
}
