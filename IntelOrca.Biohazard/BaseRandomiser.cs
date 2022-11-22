using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IntelOrca.Biohazard
{
    public abstract class BaseRandomiser
    {
        public abstract bool ValidateGamePath(string path);

        protected abstract string GetDataPath(string installPath);
        protected abstract string[] GetRdtPaths(string installPath, int player);

        protected virtual Dictionary<RdtId, ulong>? GetRdtChecksums(int player) => null;

        public int DoIntegrityCheck(string installPath)
        {
            var result = 0;
            var dataPath = GetDataPath(installPath);
            for (int player = 0; player < 2; player++)
            {
                var rdtPaths = GetRdtPaths(dataPath, player);
                var actualChecksums = GameDataReader.GetRdtChecksums(rdtPaths, player);
                var expectedChecksums = GetRdtChecksums(player);
                if (expectedChecksums == null)
                    continue;

                foreach (var kvp in expectedChecksums)
                {
                    if (actualChecksums.TryGetValue(kvp.Key, out var actualChecksum))
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

        public void Generate(RandoConfig config, string installPath)
        {
            if (config.Version < RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with an older version of the randomizer and cannot be played.");
            }
            else if (config.Version != RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with a newer version of the randomizer and cannot be played.");
            }

            var originalDataPath = GetDataPath(installPath);
            var modPath = Path.Combine(installPath, @"mod_biorand");

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

            Generate(config, originalDataPath, modPath);

            File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BioRand: A Resident Evil Randomizer\n");
        }

        protected abstract void Generate(RandoConfig config, string installPath, string modPath);

        public void GenerateRdts(RandoConfig config, string originalDataPath, string modPath)
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
                var map = GetMap();
                var gameData = GameDataReader.Read(originalDataPath, modPath, config.Player);

#if DEBUG
                DumpScripts(gameData, Path.Combine(modPath, $"scripts_pl{config.Player}"));
#endif

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
                var moddedGameData = GameDataReader.Read(modPath, modPath, config.Player);
                DumpScripts(moddedGameData, Path.Combine(modPath, $"scripts_modded_pl{config.Player}"));
#endif
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.Message);
                throw;
            }
        }

        protected abstract string GetPlayerName(int player);
        private string GetScenarioName(int scenario) => scenario == 0 ? "A" : "B";

        private Map GetMap()
        {
#if DEBUG
            return GetMapFromJson();
#else
            if (g_map == null)
            {
                g_map = LoadJsonMap();
            }
            return g_map;
#endif
        }

        private Map GetMapFromJson()
        {
            var jsonMap = GetJsonMap();
            var map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
            return map;
        }

        protected abstract string GetJsonMap();

        private void DumpRdtList(GameData gameData, string path)
        {
            var sb = new StringBuilder();
            foreach (var rdt in gameData.Rdts)
            {
                sb.AppendLine($"{rdt.RdtId} ():");
                AstPrinter.Print(sb, rdt.Ast!);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private void DumpRdt(StringBuilder sb, Rdt rdt)
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

        private void DumpScripts(GameData gameData, string scriptPath)
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
