using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.RE2;

namespace IntelOrca.Biohazard
{
    public abstract class BaseRandomiser
    {
        private List<RandomInventory?> _inventories { get; } = new List<RandomInventory?>();

        protected abstract BioVersion BiohazardVersion { get; }
        internal abstract IItemHelper ItemHelper { get; }
        internal abstract IEnemyHelper EnemyHelper { get; }
        internal abstract INpcHelper NpcHelper { get; }

        public abstract bool ValidateGamePath(string path);

        protected abstract string GetDataPath(string installPath);
        protected abstract RdtId[] GetRdtIds(string dataPath);
        protected abstract string GetRdtPath(string dataPath, RdtId rdtId, int player);

        internal DataManager DataManager
        {
            get
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var basePath = assemblyDir;
#if DEBUG
                basePath = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\IntelOrca.Biohazard"));
#endif
                var dataPath = Path.Combine(basePath, "data");
                return new DataManager(dataPath);
            }
        }

        private void SetInventory(int player, RandomInventory? inventory)
        {
            if (inventory == null)
                return;

            lock (_inventories)
            {
                while (_inventories.Count <= player)
                {
                    _inventories.Add(null);
                }
                _inventories[player] = inventory;
            }
        }

        public int DoIntegrityCheck(string installPath)
        {
            var result = 0;
            var dataPath = GetDataPath(installPath);
            var rdtIds = GetRdtIds(dataPath);
            for (int player = 0; player < 2; player++)
            {
                var actualChecksums = GetRdtChecksums(dataPath, player, rdtIds);
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

        private Dictionary<RdtId, ulong>? GetRdtChecksums(int player)
        {
            var path = DataManager.GetPath(BiohazardVersion, "checksum.json");
            if (File.Exists(path))
            {
                var checksumJson = File.ReadAllBytes(path);
                var checksumsForEachPlayer = JsonSerializer.Deserialize<Dictionary<string, ulong>[]>(checksumJson)!;
                var checksums = checksumsForEachPlayer[player];
                return checksums.ToDictionary(x => RdtId.Parse(x.Key), x => x.Value);
            }
            return null;
        }

        private Dictionary<RdtId, ulong> GetRdtChecksums(string dataPath, int player, RdtId[] rdtIds)
        {
            var result = new Dictionary<RdtId, ulong>();
            foreach (var rdtId in rdtIds)
            {
                var rdtPath = GetRdtPath(dataPath, rdtId, player);
                try
                {
                    if (File.Exists(rdtPath))
                    {
                        var rdtFile = new RdtFile(rdtPath, BiohazardVersion);
                        result[rdtId] = rdtFile.Checksum;
                    }
                }
                catch
                {
                }
            }
            return result;
        }

        private GameData ReadGameData(string dataPath, string modDataPath, int player, RdtId[] rdtIds)
        {
            var rdts = new List<Rdt>();
            foreach (var rdtId in rdtIds)
            {
                var rdtPath = GetRdtPath(dataPath, rdtId, player);
                var modRdtPath = GetRdtPath(modDataPath, rdtId, player);
                try
                {
                    var rdt = GameDataReader.ReadRdt(BiohazardVersion, rdtPath, modRdtPath);
                    rdts.Add(rdt);
                }
                catch
                {
                }
            }
            return new GameData(rdts.ToArray());
        }

        public void Generate(RandoConfig config, ReInstallConfig reConfig)
        {
            if (config.Version < RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with an older version of the randomizer and cannot be played.");
            }
            else if (config.Version != RandoConfig.LatestVersion)
            {
                throw new BioRandVersionException($"This seed was generated with a newer version of the randomizer and cannot be played.");
            }

            var installPath = reConfig.GetInstallPath(BiohazardVersion);
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

            Generate(config, reConfig, originalDataPath, modPath);

            File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BioRand: A Resident Evil Randomizer\n");
        }

        public virtual void Generate(RandoConfig config, ReInstallConfig reConfig, string installPath, string modPath)
        {
            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading("Resident Evil Randomizer");
                logger.WriteLine($"Seed: {config}");

                var bgmDirectory = Path.Combine(modPath, BGMPath);
                var bgmRandomizer = new BgmRandomiser(logger, bgmDirectory, GetBgmJson(), BiohazardVersion == BioVersion.Biohazard1, new Rng(config.Seed), DataManager);
                if (config.IncludeBGMRE1)
                {
                    var r = new Re1Randomiser();
                    r.AddMusicSelection(bgmRandomizer, reConfig);
                }
                if (config.IncludeBGMRE2)
                {
                    var r = new Re2Randomiser();
                    r.AddMusicSelection(bgmRandomizer, reConfig);
                }
                bgmRandomizer.AddCutomMusicToSelection(config);

                if (BiohazardVersion == BioVersion.Biohazard1)
                {
                    bgmRandomizer.ImportVolume = 0.25f;
                }

                bgmRandomizer.Randomise();
            }

            RandoBgCreator.Save(config, modPath, BiohazardVersion, DataManager);
        }

        public void GenerateRdts(RandoConfig config, string originalDataPath, string modPath)
        {
            var baseSeed = config.Seed + config.Player;
            var randomDoors = new Rng(baseSeed + 1);
            var randomItems = new Rng(baseSeed + 2);
            var randomEnemies = new Rng(baseSeed + 3);
            var randomNpcs = new Rng(baseSeed + 4);

            // In RE1, room sounds are not in the RDT, so enemies must be same for both players
            if (BiohazardVersion == BioVersion.Biohazard1)
                randomEnemies = new Rng(config.Seed + 3);

            using var logger = new RandoLogger(Path.Combine(modPath, $"log_pl{config.Player}.txt"));
            logger.WriteHeading("Resident Evil Randomizer");
            logger.WriteLine($"Seed: {config}");
            logger.WriteLine($"Player: {config.Player} {GetPlayerName(config.Player)}");
            logger.WriteLine($"Scenario: {GetScenarioName(config.Scenario)}");

            try
            {
                var rdtIds = GetRdtIds(originalDataPath);
                var gameData = ReadGameData(originalDataPath, modPath, config.Player, rdtIds);

#if DEBUG
                DumpScripts(config, gameData, Path.Combine(modPath, $"scripts_pl{config.Player}"));
#endif

                var map = GetMapFromJson();
                if (config.RandomDoors || config.RandomItems)
                {
                    var dgmlPath = Path.Combine(modPath, $"graph_pl{config.Player}.dgml");
                    var doorRando = new DoorRandomiser(logger, config, gameData, map, randomDoors, ItemHelper);
                    var itemRando = new ItemRandomiser(logger, config, gameData, randomItems, ItemHelper);
                    var graph = config.RandomDoors ?
                        doorRando.CreateRandomGraph() :
                        doorRando.CreateOriginalGraph();
                    try
                    {
                        itemRando.RandomiseItems(graph);
                        SetInventory(config.Player, itemRando.RandomizeStartInventory());
                    }
                    finally
                    {
                        graph.GenerateDgml(dgmlPath, ItemHelper);
                    }
                }

                if (config.RandomEnemies)
                {
                    var enemyRandomiser = new EnemyRandomiser(BiohazardVersion, logger, config, gameData, map, randomEnemies, EnemyHelper, modPath, DataManager);
                    enemyRandomiser.Randomise();
                }

                if (config.RandomNPCs)
                {
                    var npcRandomiser = new NPCRandomiser(BiohazardVersion, logger, config, originalDataPath, modPath, gameData, map, randomNpcs, NpcHelper, DataManager);
                    RandomizeNPCs(config, npcRandomiser);
                    npcRandomiser.Randomise();
                }

                foreach (var rdt in gameData.Rdts)
                {
                    rdt.Save();
                }

#if DEBUG
                var moddedGameData = ReadGameData(modPath, modPath, config.Player, rdtIds);
                DumpScripts(config, moddedGameData, Path.Combine(modPath, $"scripts_modded_pl{config.Player}"));
#endif
            }
            catch (Exception ex)
            {
                logger.WriteException(ex);
                throw;
            }
        }

        internal virtual void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser)
        {
        }

        public abstract string GetPlayerName(int player);
        private string GetScenarioName(int scenario) => scenario == 0 ? "A" : "B";

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

        private string GetJsonMap()
        {
            var path = DataManager.GetPath(BiohazardVersion, "rdt.json");
            return File.ReadAllText(path);
        }

        internal string GetBgmJson() => DataManager.GetText(BiohazardVersion, "bgm.json");

        internal abstract string BGMPath { get; }

        private void DumpRdtList(GameData gameData, string path)
        {
            var sb = new StringBuilder();
            foreach (var rdt in gameData.Rdts)
            {
                sb.AppendLine($"{rdt.RdtId} ():");
                Script.AstPrinter.Print(sb, rdt.Ast!);
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

        private void DumpScripts(RandoConfig config, GameData gameData, string scriptPath)
        {
            Directory.CreateDirectory(scriptPath);
            foreach (var rdt in gameData.Rdts)
            {
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.bio"), rdt.Script);
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.s"), rdt.ScriptDisassembly);
            }

            // var player = config.Player;
            // var options = new JsonSerializerOptions()
            // {
            //     ReadCommentHandling = JsonCommentHandling.Skip
            // };
            // var old = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(@"M:\git\rer\IntelOrca.Biohazard\data\voice.json"), options)!;
            // var sb = new StringBuilder();
            // var done = new HashSet<string>();
            // foreach (var rdt in gameData.Rdts)
            // {
            //     foreach (var sound in rdt.Sounds)
            //     {
            //         var stage = rdt.RdtId.Stage;
            //         var id = sound.Id;
            //         var path = $"pl{player}/voice/stage{stage + 1}/v{id:000}.sap";
            //         if (done.Add(path) && old.TryGetValue(path, out var value))
            //         {
            //             var actor = value;
            //             sb.AppendLine($"\"{path}\": {{ \"rdt\": \"{rdt.RdtId}\", \"player\": {player}, \"actor\": \"{actor}\" }},");
            //         }
            //     }
            // }
        }

        protected void SerialiseInventory(string modPath)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Init");
            foreach (var inventory in _inventories.Reverse<RandomInventory?>())
            {
                var playerNode = doc.CreateElement("Player");
                if (inventory != null)
                {
                    foreach (var entry in inventory.Entries)
                    {
                        var entryNode = doc.CreateElement("Entry");
                        entryNode.SetAttribute("id", entry.Type.ToString());
                        entryNode.SetAttribute("count", entry.Count.ToString());
                        playerNode.AppendChild(entryNode);
                    }
                }
                root.AppendChild(playerNode);
            }
            doc.AppendChild(root);
            doc.Save(Path.Combine(modPath, "init.xml"));
        }

        public virtual string[] GetPlayerCharacters(int index)
        {
            return new string[0];
        }
    }
}
