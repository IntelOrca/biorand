using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.RE2;
using IntelOrca.Biohazard.RE3;

namespace IntelOrca.Biohazard
{
    public abstract class BaseRandomiser
    {
        protected IBgCreator? BgCreator { get; }

        protected MemoryStream ExePatch { get; } = new MemoryStream();
        internal List<RandomInventory?> Inventories { get; } = new List<RandomInventory?>();

        protected abstract BioVersion BiohazardVersion { get; }
        internal abstract IItemHelper ItemHelper { get; }
        internal abstract IEnemyHelper EnemyHelper { get; }
        internal abstract INpcHelper NpcHelper { get; }

        public abstract bool ValidateGamePath(string path);

        protected abstract string GetDataPath(string installPath);
        protected abstract RdtId[] GetRdtIds(string dataPath);
        protected abstract string GetRdtPath(string dataPath, RdtId rdtId, int player);

        public BaseRandomiser(IBgCreator? bgCreator)
        {
            BgCreator = bgCreator;
        }

        internal DataManager DataManager
        {
            get
            {
                var dataPath = Environment.GetEnvironmentVariable("BIORAND_DATA");
                if (string.IsNullOrEmpty(dataPath))
                {
                    var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var basePath = assemblyDir;
#if DEBUG
                    basePath = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\IntelOrca.Biohazard"));
#endif
                    dataPath = Path.Combine(basePath, "data");
                }
                return new DataManager(dataPath);
            }
        }

        private void SetInventory(int player, RandomInventory? inventory)
        {
            if (inventory == null)
                return;

            lock (Inventories)
            {
                while (Inventories.Count <= player)
                {
                    Inventories.Add(null);
                }
                Inventories[player] = inventory;
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

        public void Generate(RandoConfig config, ReInstallConfig reConfig, IRandoProgress progress)
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

            using (progress.BeginTask(null, $"Clearing '{modPath}'"))
            {
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
            }

            using (progress.BeginTask(config.Player, $"Generating seed"))
                Generate(config, reConfig, progress, originalDataPath, modPath);

            using (progress.BeginTask(null, $"Writing manifest"))
            {
                File.WriteAllText(Path.Combine(modPath, "manifest.txt"), "[MOD]\nName = BioRand: A Resident Evil Randomizer\nModule = biorand.dll\n");
                File.WriteAllText(Path.Combine(modPath, "description.txt"), $"{Program.CurrentVersionInfo}\r\nSeed: {config}\r\n");
            }
        }

        public virtual void Generate(RandoConfig config, ReInstallConfig reConfig, IRandoProgress progress, string installPath, string modPath)
        {
            if (config.RandomItems && config.RandomInventory)
            {
                SerialiseInventory(modPath);
            }

            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(progress, Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading(Program.CurrentVersionInfo);
                logger.WriteLine($"Seed: {config}");

                var fileRepo = new FileRepository();
                var bgmDirectory = Path.Combine(modPath, BGMPath);
                var bgmRandomizer = new BgmRandomiser(logger, config, fileRepo, bgmDirectory, GetBgmJson(), BiohazardVersion == BioVersion.Biohazard1, new Rng(config.Seed), DataManager);
                var enabledBgms = GetSelectedAlbums(config);
                if (enabledBgms.Contains("RE1", StringComparer.OrdinalIgnoreCase))
                {
                    var r = new Re1Randomiser(BgCreator);
                    r.AddMusicSelection(bgmRandomizer, reConfig);
                }
                if (enabledBgms.Contains("RE2", StringComparer.OrdinalIgnoreCase))
                {
                    var r = new Re2Randomiser(BgCreator);
                    r.AddMusicSelection(bgmRandomizer, reConfig);
                }
                if (enabledBgms.Contains("RE3", StringComparer.OrdinalIgnoreCase))
                {
                    var r = new Re3Randomiser(BgCreator);
                    r.AddArchives(reConfig, fileRepo);
                    r.AddMusicSelection(bgmRandomizer, reConfig);
                }
                bgmRandomizer.AddCutomMusicToSelection(enabledBgms);

                if (BiohazardVersion == BioVersion.Biohazard1)
                {
                    bgmRandomizer.ImportVolume = 0.25f;
                }

                using (progress.BeginTask(null, "Randomizing BGM"))
                {
                    bgmRandomizer.Randomise();
                }
            }

            using (progress.BeginTask(null, $"Creating backgrounds"))
                CreateBackgrounds(config, modPath);
            using (progress.BeginTask(null, $"Copying title card sounds"))
                CreateTitleCardSounds(config, modPath);
            using (progress.BeginTask(null, $"Copying biorand.dll"))
                CreateDataModule(modPath);
        }

        public void GenerateRdts(RandoConfig config, IRandoProgress progress, string originalDataPath, string modPath)
        {
            var baseSeed = config.Seed + config.Player;
            var randomDoors = new Rng(baseSeed + 1);
            var randomItems = new Rng(baseSeed + 2);
            var randomEnemies = new Rng(baseSeed + 3);
            var randomNpcs = new Rng(baseSeed + 4);

            // In RE1, room sounds are not in the RDT, so enemies must be same for both players
            if (BiohazardVersion == BioVersion.Biohazard1)
                randomEnemies = new Rng(config.Seed + 3);

            using var logger = new RandoLogger(progress, Path.Combine(modPath, $"log_pl{config.Player}.txt"));
            logger.WriteHeading(Program.CurrentVersionInfo);
            logger.WriteLine($"Seed: {config}");
            logger.WriteLine($"Player: {config.Player} {GetPlayerName(config.Player)}");
            logger.WriteLine($"Scenario: {GetScenarioName(config.Scenario)}");

            try
            {
                var rdtIds = GetRdtIds(originalDataPath);
                var gameData = ReadGameData(originalDataPath, modPath, config.Player, rdtIds);

#if DEBUG
                using (progress.BeginTask(config.Player, "Dumping original RDT disassemblies"))
                {
                    DumpScripts(config, gameData, Path.Combine(modPath, $"scripts_pl{config.Player}"));
                }
#endif

                var map = GetMapFromJson();
                var graph = null as PlayGraph;
                if (config.RandomDoors || config.RandomItems)
                {
                    var dgmlPath = Path.Combine(modPath, $"graph_pl{config.Player}.dgml");
                    var doorRando = new DoorRandomiser(logger, config, gameData, map, randomDoors, ItemHelper);
                    var itemRando = new ItemRandomiser(logger, config, gameData, randomItems, ItemHelper);
                    using (progress.BeginTask(config.Player, "Randomizing doors"))
                    {
                        graph = config.RandomDoors ?
                        doorRando.CreateRandomGraph() :
                        doorRando.CreateOriginalGraph();
                    }
                    try
                    {
                        using (progress.BeginTask(config.Player, "Randomizing items"))
                        {
                            itemRando.RandomiseItems(graph);
                        }
                        if (config.RandomInventory)
                        {
                            SetInventory(config.Player, itemRando.RandomizeStartInventory());
                        }
                    }
                    finally
                    {
                        graph.GenerateDgml(dgmlPath, ItemHelper);
                    }
                }

                if (config.RandomEnemies)
                {
                    using (progress.BeginTask(config.Player, "Randomizing enemies"))
                    {
                        var enemyRandomiser = new EnemyRandomiser(BiohazardVersion, logger, config, gameData, map, randomEnemies, EnemyHelper, modPath, DataManager);
                        enemyRandomiser.Randomise(graph);
                    }
                }

                string? playerActor;
                using (progress.BeginTask(config.Player, $"Changing player character"))
                    playerActor = ChangePlayerCharacters(config, logger, gameData, originalDataPath, modPath);
                if (config.RandomNPCs)
                {
                    using (progress.BeginTask(config.Player, "Randomizing NPCs"))
                    {
                        var npcRandomiser = new NPCRandomiser(BiohazardVersion, logger, config, originalDataPath, modPath, gameData, map, randomNpcs, NpcHelper, DataManager, playerActor);
                        npcRandomiser.SelectedActors.AddRange(GetSelectedActors(config));
                        RandomizeNPCs(config, npcRandomiser);
                        npcRandomiser.Randomise();
                    }
                }

                using (progress.BeginTask(config.Player, "Writing RDT files"))
                {
                    foreach (var rdt in gameData.Rdts)
                    {
                        rdt.Save();
                    }
                }

#if DEBUG
                using (progress.BeginTask(config.Player, "Dumping modified RDT disassemblies"))
                {
                    var moddedGameData = ReadGameData(modPath, modPath, config.Player, rdtIds);
                    DumpScripts(config, moddedGameData, Path.Combine(modPath, $"scripts_modded_pl{config.Player}"));
                }
#endif
            }
            catch (Exception ex)
            {
                logger.WriteException(ex);
                throw;
            }
        }

        protected virtual string[] GetTitleCardSoundFiles(string modPath)
        {
            return new string[0];
        }

        internal virtual string? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, string originalDataPath, string modPath)
        {
            return null;
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
                File.WriteAllText(Path.Combine(scriptPath, $"{rdt.RdtId}.lst"), rdt.ScriptListing);
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

        protected virtual void SerialiseInventory(string modPath)
        {
        }

        public virtual string[] GetPlayerCharacters(int index)
        {
            return new string[0];
        }

        protected string GetSelectedActor(RandoConfig config)
        {
            return GetSelectedActor(config, config.Player);
        }

        protected string GetSelectedActor(RandoConfig config, int player)
        {
            if (!config.ChangePlayer)
                return GetPlayerName(player).ToLower();

            var pldIndex = player == 0 ? config.Player0 : config.Player1;
            var pldPath = DataManager.GetDirectories(BiohazardVersion, $"pld{player}")
                .Skip(pldIndex)
                .FirstOrDefault();
            return Path.GetFileName(pldPath).ToLower();
        }

        public virtual string[] GetNPCs()
        {
            var actors = new HashSet<string>();
            actors.AddRange(GetDefaultNPCs());
            for (int i = 0; i < 2; i++)
            {
                var emds = DataManager
                    .GetDirectories(BiohazardVersion, $"emd")
                    .Select(Path.GetFileName)
                    .ToArray();
                actors.AddRange(emds);
            }
            return actors
                .OrderBy(x => x)
                .ToArray();
        }

        protected virtual string[] GetDefaultNPCs()
        {
            return new[] { "chris", "jill", "barry", "rebecca", "wesker", "enrico", "richard" };
        }

        protected bool MusicAlbumSelected(RandoConfig config, string album)
        {
            var albumIndex = Array.FindIndex(GetMusicAlbums(), x => x.Equals(album, StringComparison.OrdinalIgnoreCase));
            if (albumIndex > -1 && albumIndex < config.EnabledBGMs.Length)
            {
                return config.EnabledBGMs[albumIndex];
            }
            return false;
        }

        public virtual string[] GetMusicAlbums()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "re1",
                "re2"
            };
            result.AddRange(DataManager
                .GetDirectoriesIn("bgm")
                .Select(Path.GetFileName));
            return result
                .Select(x => x.ToUpper())
                .OrderBy(x => x)
                .ToArray();
        }

        private string[] GetSelectedActors(RandoConfig config)
        {
            var enabledNpcs = config.EnabledNPCs;
            var npcs = GetNPCs();
            for (int i = 0; i < npcs.Length; i++)
            {
                if (enabledNpcs.Length <= i || !enabledNpcs[i])
                {
                    npcs[i] = "";
                }
            }
            return npcs
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        private string[] GetSelectedAlbums(RandoConfig config)
        {
            var enabledBgms = config.EnabledBGMs;
            var albums = GetMusicAlbums();
            for (int i = 0; i < albums.Length; i++)
            {
                if (enabledBgms.Length <= i || !enabledBgms[i])
                {
                    albums[i] = "";
                }
            }
            return albums
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        public SelectableEnemy[] GetEnemies()
        {
            return EnemyHelper.GetSelectableEnemies();
        }

        private void CreateBackgrounds(RandoConfig config, string modPath)
        {
            try
            {
                var src = DataManager.GetData(BiohazardVersion, "bg.png");
                if (BiohazardVersion == BioVersion.Biohazard1)
                {
                    CreateBackgroundRaw(config, src, Path.Combine(modPath, @"data\title.pix"));
                    CreateBackgroundPng(config, src, Path.Combine(modPath, @"type.png"));
                }
                else
                {
                    CreateBackgroundPng(config, src, Path.Combine(modPath, @"common\data\title_bg.png"));
                    CreateBackgroundPng(config, src, Path.Combine(modPath, @"common\data\type00.png"));
                }
            }
            catch
            {
            }
        }

        private void CreateBackgroundRaw(RandoConfig config, byte[] pngBackground, string outputFilename)
        {
            if (BgCreator == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilename));
            var pixels = BgCreator.CreateARGB(config, pngBackground);
            using var fs = new FileStream(outputFilename, FileMode.Create);
            var bw = new BinaryWriter(fs);

            for (int i = 0; i < 320 * 240; i++)
            {
                var c = pixels[i];
                var r = (byte)((c >> 16) & 0xFF);
                var g = (byte)((c >> 8) & 0xFF);
                var b = (byte)((c >> 0) & 0xFF);
                var c4 = (ushort)((r / 8) | ((g / 8) << 5) | ((b / 8) << 10));
                bw.Write(c4);
            }
        }

        private void CreateBackgroundPng(RandoConfig config, byte[] pngBackground, string outputFilename)
        {
            if (BgCreator == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilename));
            var titleBg = BgCreator.CreatePNG(config, pngBackground);
            File.WriteAllBytes(outputFilename, titleBg);
        }

        private void CreateTitleCardSounds(RandoConfig config, string modPath)
        {
            // Title sound
            var targetTitleCardSounds = GetTitleCardSoundFiles(modPath);
            if (targetTitleCardSounds.Length != 0)
            {
                var titleSounds = DataManager
                    .GetFiles("title")
                    .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    .Where(x => !Path.GetFileName(x).Equals("template.ogg", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (titleSounds.Length != 0)
                {
                    var rng = new Rng(config.Seed);
                    var titleSound = rng.NextOf(titleSounds);
                    var builder = new WaveformBuilder();
                    builder.Append(titleSound);

                    var coreDirectory = Path.Combine(modPath, "Common/Sound/core");
                    foreach (var dst in targetTitleCardSounds)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        builder.Save(dst);
                    }
                }
            }
        }

        private void CreateDataModule(string modPath)
        {
            File.WriteAllBytes(Path.Combine(modPath, "biorand.dat"), ExePatch.ToArray());
            var biorandModuleFilename = "biorand.dll";
            try
            {
                File.Copy(DataManager.GetPath(biorandModuleFilename), Path.Combine(modPath, biorandModuleFilename), true);
            }
            catch
            {
            }
        }
    }
}
