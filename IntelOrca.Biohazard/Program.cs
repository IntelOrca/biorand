using System.IO;
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

        public static void Main(string[] args)
        {
            var re2Path = @"F:\games\re2";
            var originalDataPath = Path.Combine(re2Path, "data");
            var modPath = Path.Combine(re2Path, @"mod_rando");

            var gameData = GameDataReader.Read(originalDataPath, modPath, 0);
            DumpRdtList(gameData, @"M:\git\rer\docs\rdt_leon.txt");
            DumpScripts(gameData, @"F:\games\re2\mod_rando\scripts");

            gameData = GameDataReader.Read(originalDataPath, modPath, 1);
            DumpRdtList(gameData, @"M:\git\rer\docs\rdt_claire.txt");
        }

        public static void Generate(RandoConfig config, string re2Path)
        {
            var originalDataPath = Path.Combine(re2Path, "data");
            var modPath = Path.Combine(re2Path, @"mod_rando");

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
                bgmRandomiser.Randomise(new Rng(config.Seed + 3));
            }

            File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BIOHAZARD 2: RANDOMIZER\n");
        }

        public static void GenerateRdts(RandoConfig config, string originalDataPath, string modPath)
        {
            var randomItems = new Rng(config.Seed);
            var randomNpcs = new Rng(config.Seed + 1);
            var randomEnemies = new Rng(config.Seed + 2);

            using var logger = new RandoLogger(Path.Combine(modPath, $"log_pl{config.Player}.txt"));
            logger.WriteHeading("Resident Evil Randomizer");
            logger.WriteLine($"Seed: {config}");
            logger.WriteLine($"Player: {config.Player} {GetPlayerName(config.Player)}");
            logger.WriteLine($"Scenario: {GetScenarioName(config.Scenario)}");

            var map = GetJsonMap();
            var gameData = GameDataReader.Read(originalDataPath, modPath, config.Player);

            if (config.RandomItems)
            {
                var factory = new PlayGraphFactory(logger, config, gameData, map, randomItems);
                // CheckRoomItems(gameData);
                // factory.CreateDoorRando();
                factory.Create();
                factory.SetItems();
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
                DumpScripts(gameData, Path.Combine(modPath, "scripts_pl0"));
                var moddedGameData = GameDataReader.Read(modPath, modPath, config.Player);
                DumpScripts(moddedGameData, Path.Combine(modPath, "scripts_modded"));
            }
#endif
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
            var jsonMap = File.ReadAllText(@"M:\git\rer\rer\data\rdt.json");
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
                sb.AppendLine($"    Door #{door.Id}: {new RdtId(door.Stage, door.Room)} (0x{door.Offset:X2})");
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
