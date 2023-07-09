using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.RE3;

namespace IntelOrca.Biohazard.RE1
{
    public class Re1Randomiser : BaseRandomiser
    {
        private readonly Re1EnemyHelper _enemyHelper = new Re1EnemyHelper();
        private ReInstallConfig? _reInstallConfig;

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard1;
        internal override IDoorHelper DoorHelper { get; } = new Re1DoorHelper();
        internal override IItemHelper ItemHelper { get; } = new Re1ItemHelper();
        internal override IEnemyHelper EnemyHelper => _enemyHelper;
        internal override INpcHelper NpcHelper { get; } = new Re1NpcHelper();

        public Re1Randomiser(IBgCreator? bgCreator) : base(bgCreator)
        {
        }

        public override string GetPlayerName(int player) => player == 0 ? "Chris" : "Jill";

        protected override string[] GetDefaultNPCs()
        {
            return new[] { "chris", "jill", "barry", "rebecca", "wesker", "enrico", "richard" };
        }

        public override bool ValidateGamePath(string path)
        {
            var dataPath = FindDataPath(path);
            return Directory.Exists(Path.Combine(dataPath, "STAGE1"));
        }

        protected override string GetDataPath(string installPath) => FindDataPath(installPath);

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            var rdtIds = new HashSet<RdtId>();
            for (int stage = 1; stage <= 7; stage++)
            {
                var files = Directory.GetFiles(Path.Combine(dataPath, @$"STAGE{stage}"));
                foreach (var file in files)
                {
                    // Check the file is an RDT file
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                        !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (RdtId.TryParse(fileName.Substring(4, 3), out var rdtId))
                    {
                        rdtIds.Add(rdtId);
                    }
                }
            }
            return rdtIds
                .OrderBy(x => x.Stage)
                .ThenBy(x => x.Room)
                .Except(new[] {
                    new RdtId(0, 0x10),
                    new RdtId(0, 0x19),
                    new RdtId(1, 0x00),
                    new RdtId(1, 0x0C),
                    new RdtId(1, 0x13),
                    new RdtId(1, 0x14),
                    new RdtId(1, 0x15),
                    new RdtId(1, 0x16),
                    new RdtId(1, 0x17),
                    new RdtId(1, 0x18),
                    new RdtId(1, 0x19),
                    new RdtId(1, 0x1A),
                    new RdtId(1, 0x1B),
                    new RdtId(1, 0x1C)
                })
                .ToArray();
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player)
        {
            var path = Path.Combine(dataPath, @$"STAGE{rdtId.Stage + 1}\ROOM{rdtId}{player}.RDT");
            return path;
        }

        public override void Generate(RandoConfig config, ReInstallConfig reConfig, IRandoProgress progress, FileRepository fileRepository)
        {
            _reInstallConfig = reConfig;

            if (!reConfig.IsEnabled(BioVersion.Biohazard1))
            {
                throw new BioRandUserException("RE1 installation must be enabled to randomize RE1.");
            }
            if (config.RandomBgm && MusicAlbumSelected(config, "RE2"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard2))
                {
                    throw new BioRandUserException("RE2 installation must be enabled to use RE2 assets.");
                }
            }
            if (config.RandomBgm && MusicAlbumSelected(config, "RE3"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard3))
                {
                    throw new BioRandUserException("RE3 installation must be enabled to use RE3 assets.");
                }
            }

            var po = new ParallelOptions();
#if DEBUG
            po.MaxDegreeOfParallelism = 1;
#endif
            // Chris / Jill
            Parallel.Invoke(po,
                () => GenerateRdts(config.WithPlayerScenario(0, 0), progress, fileRepository),
                () => GenerateRdts(config.WithPlayerScenario(1, 0), progress, fileRepository));

            if (config.ChangePlayer)
            {
                ChangePlayerInventoryFace(config, fileRepository);
            }

            FixWeaponHitScan(config);

            base.Generate(config, reConfig, progress, fileRepository);
        }

        protected override string[] TitleCardSoundFiles { get; } =
            new[] {
                "sound/BIO01.WAV",
                "sound/EVIL01.WAV"
            };

        internal override string[] ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            var actor = config.Player == 0 ? "chris" : "jill";
            var partner = config.Player == 0 ? "rebecca" : "barry";
            if (config.ChangePlayer)
            {
                var pldPath = GetSelectedPldPath(config, config.Player);
                actor = Path.GetFileName(pldPath);
                SwapPlayerCharacter(config, logger, actor, fileRepository);
            }
            return new[] { actor, partner };
        }

        private void ChangePlayerInventoryFace(RandoConfig config, FileRepository fileRepository)
        {
            if (BgCreator == null)
                return;

            var inputTimPath = fileRepository.GetDataPath("data/statface.tim");
            var outputTimPath = fileRepository.GetModPath("data/statface.tim");
            Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

            var timFile = new TimFile(inputTimPath);

            for (int i = 0; i < 2; i++)
            {
                var pldPath = GetSelectedPldPath(config, i);
                var facePath = DataManager.GetPath(BiohazardVersion, Path.Combine(pldPath, "face.png"));
                if (File.Exists(facePath))
                {
                    BgCreator.DrawImage(timFile, facePath, i * 32, 0);
                }
            }

            timFile.Save(outputTimPath);
        }

        public override string[] GetPlayerCharacters(int index)
        {
            var result = new List<string>();
            var pldFiles = DataManager
                .GetDirectories(BiohazardVersion, $"pld{index}")
                .ToArray();
            foreach (var pldPath in pldFiles)
            {
                var actor = Path.GetFileName(pldPath);
                result.Add(actor.ToActorString());
            }
            return result.ToArray();
        }

        public override EnemySkin[] GetEnemySkins()
        {
            var emdRegex = new Regex("em10([0-9a-f][0-9a-f]).emd", RegexOptions.IgnoreCase);
            var result = new List<EnemySkin>();
            result.Add(EnemySkin.Original);
            foreach (var enemyDir in DataManager.GetDirectories(BiohazardVersion, "emd"))
            {
                var enemyIds = new List<byte>();
                foreach (var file in Directory.GetFiles(enemyDir))
                {
                    var fileName = Path.GetFileName(file);
                    var match = emdRegex.Match(fileName);
                    if (match.Success)
                    {
                        var id = byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                        enemyIds.Add(id);
                    }
                }
                if (enemyIds.Count > 0)
                {
                    var fileName = Path.GetFileName(enemyDir);
                    var enemyNames = enemyIds
                        .Select(x => EnemyHelper.GetEnemyName(x).ToLower().ToActorString())
                        .ToArray();
                    result.Add(new EnemySkin(fileName, enemyNames, enemyIds.ToArray()));
                }
            }
            return result
                .OrderBy(x => x.IsOriginal ? 0 : 1)
                .ThenBy(x => x.IsNPC ? 0 : 1)
                .ToArray();
        }

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, string actor, FileRepository fileRepository)
        {
            var originalPlayerActor = config.Player == 0 ? "chris" : "jill";
            var srcPldDir = GetSelectedPldPath(config, config.Player);
            var srcFacePath = DataManager.GetPath(BiohazardVersion, $"face\\{actor}.tim");

            if (originalPlayerActor != actor)
            {
                logger.WriteHeading("Randomizing Player:");
                logger.WriteLine($"{originalPlayerActor} becomes {actor}");
            }

            var targetEnemyDir = fileRepository.GetModPath("enemy");
            var targetPlayersDir = fileRepository.GetModPath("players");
            Directory.CreateDirectory(targetEnemyDir);
            Directory.CreateDirectory(targetPlayersDir);

            // Copy base EMW files
            var baseWeaponIndex = (config.SwapCharacters ? config.Player ^ 1 : config.Player) * 16;
            for (var i = 0; i < 16; i++)
            {
                var sourceFileName = $"players/w{baseWeaponIndex + i:X2}.emw";
                var targetFileName = $"players/w{(config.Player * 16) + i:X2}.emw";
                var sourceFile = fileRepository.GetDataPath(sourceFileName);
                var targetLocation = fileRepository.GetModPath(targetFileName);
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, targetLocation, true);
                }
            }

            // Copy override EMD files
            var pldFiles = Directory.GetFiles(srcPldDir);
            foreach (var pldFile in pldFiles)
            {
                var fileName = Path.GetFileName(pldFile);
                if (Regex.IsMatch(fileName, "char1[01].emd", RegexOptions.IgnoreCase))
                {
                    var targetFileName = config.Player == 0 ? "CHAR10.EMD" : "CHAR11.EMD";
                    File.Copy(pldFile, Path.Combine(targetEnemyDir, targetFileName), true);
                    continue;
                }

                var regex = Regex.Match(fileName, "w([0-9a-f][0-9a-f]).emw", RegexOptions.IgnoreCase);
                if (regex.Success)
                {
                    var originalIndex = Convert.ToInt32(regex.Groups[1].Value, 16);
                    var weaponIndex = originalIndex % 16;
                    var targetWeaponIndex = config.Player == 0 ? weaponIndex : weaponIndex + 16;
                    var targetFileName = $"W{targetWeaponIndex:X2}.EMW";
                    File.Copy(pldFile, Path.Combine(targetPlayersDir, targetFileName), true);
                }
            }

            // Replace hurt sounds
            if (actor != originalPlayerActor)
            {
                var allHurtFiles = DataManager.GetHurtFiles(actor)
                    .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var hurtFiles = new string[4];
                foreach (var hurtFile in allHurtFiles)
                {
                    if (int.TryParse(Path.GetFileNameWithoutExtension(hurtFile), out var i))
                    {
                        if (i < hurtFiles.Length)
                        {
                            hurtFiles[i] = hurtFile;
                        }
                    }
                }
                if (hurtFiles.All(x => x != null))
                {
                    var hurtFileNames = new[]
                    {
                        new[] { "chris", "ch_ef" },
                        new[] { "jill", "jill_ef" }
                    };

                    var soundDir = fileRepository.GetModPath("sound");
                    Directory.CreateDirectory(soundDir);

                    for (int i = 0; i < hurtFiles.Length; i++)
                    {
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[i]);
                        for (var j = 0; j < 2; j++)
                        {
                            var hurtFileName = hurtFileNames[config.Player][j];
                            var soundPath = Path.Combine(soundDir, $"{hurtFileName}{i + 1:00}.WAV");
                            waveformBuilder.Save(soundPath);
                        }
                    }
                }
            }
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser, VoiceRandomiser voiceRandomiser)
        {
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard2))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard2));
                // HACK should be helper function from RE 2 randomizer
                if (Directory.Exists(Path.Combine(dataPath, "data", "pl0", "rdt")))
                {
                    dataPath = Path.Combine(dataPath, "data");
                }
                voiceRandomiser.AddToSelection(BioVersion.Biohazard2, new FileRepository(dataPath));
            }
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard3))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard3));
                var fileRepository = new FileRepository(dataPath);
                var re3randomizer = new Re3Randomiser(null);
                re3randomizer.AddArchives(dataPath, fileRepository);
                voiceRandomiser.AddToSelection(BioVersion.Biohazard3, fileRepository);
            }

            var pldFolders0 = DataManager.GetDirectories(BiohazardVersion, $"pld0");
            var pldFolders1 = DataManager.GetDirectories(BiohazardVersion, $"pld1");
            var pldFolders = pldFolders0.Concat(pldFolders1).ToArray();
            foreach (var pldFolder in pldFolders)
            {
                var actor = Path.GetFileName(pldFolder);
                var files = Directory.GetFiles(pldFolder);
                foreach (var file in files)
                {
                    if (file.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                    {
                        npcRandomiser.AddNPC(0, file, actor);
                    }
                }
            }
        }

        internal override void RandomizeEnemySkins(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            logger.WriteHeading("Randomizing enemy skins:");

            var rng = new Rng(config.Seed);

            var pldDir0 = DataManager.GetDirectories(BiohazardVersion, "pld0");
            var pldDir1 = DataManager.GetDirectories(BiohazardVersion, "pld1");
            var pldBag = new EndlessBag<string>(rng, pldDir0.Concat(pldDir1));

            var enemySkins = GetEnemySkins()
                .Zip(config.EnabledEnemySkins, (skin, enabled) => (skin, enabled))
                .Where(s => s.enabled)
                .Select(s => s.skin)
                .Shuffle(rng);

            var keepOriginal = new HashSet<byte>();
            if (enemySkins.Any(x => x.IsOriginal))
            {
                keepOriginal = enemySkins
                    .SelectMany(x => x.EnemyIds)
                    .GroupBy(x => x)
                    .Select(x => rng.Next(0, x.Count() + 1) == 0 ? x.Key : (byte)0)
                    .ToHashSet();
            }

            var soundProcessActions = new List<Action>();
            var sapLock = new object();

            var replacedEnemyTypes = new HashSet<byte>();
            foreach (var skin in enemySkins)
            {
                foreach (var id in skin.EnemyIds)
                {
                    // Check if we are to preserve the original enemy type
                    if (keepOriginal.Contains(id))
                        continue;

                    if (!replacedEnemyTypes.Add(id))
                        continue;

                    // EMD/TIM
                    var enemyDir = DataManager.GetPath(BiohazardVersion, Path.Combine("emd", skin.FileName));
                    var srcEmdFileName = $"EM10{id:X2}.EMD";
                    var dstEmdFileName = $"EM1{config.Player}{id:X2}.EMD";
                    var emdPath = $"enemy/{dstEmdFileName}";
                    var origEmd = fileRepository.GetDataPath(emdPath);
                    var srcEmd = Path.Combine(enemyDir, srcEmdFileName);
                    var dstEmd = fileRepository.GetModPath(emdPath);

                    if (new FileInfo(srcEmd).Length == 0)
                    {
                        // NPC overwrite
                        var pldFolder = pldBag.Next();
                        var actor = Path.GetFileName(pldFolder).ToActorString();
                        var pldPath = Directory.GetFiles(pldFolder)
                            .First(x => x.EndsWith(".emd", StringComparison.OrdinalIgnoreCase));
                        var pldFile = new EmdFile(BiohazardVersion, pldPath);
                        var emdFile = new EmdFile(BiohazardVersion, origEmd);

                        logger.WriteLine($"Setting EM1{config.Player}{id:X2} to {actor}");
                        _enemyHelper.CreateZombie(id, pldFile, emdFile, dstEmd);
                    }
                    else
                    {
                        logger.WriteLine($"Setting EM1{config.Player}{id:X2} to {skin.Name}");
                        Directory.CreateDirectory(Path.GetDirectoryName(dstEmd));
                        File.Copy(srcEmd, dstEmd, true);
                    }

                    // Sounds (shared, so only do it for Player 0)
                    if (config.Player == 1)
                        continue;

                    foreach (var file in Directory.GetFiles(enemyDir))
                    {
                        if (file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            var soundFileName = Path.ChangeExtension(Path.GetFileName(file), ".wav").ToUpperInvariant();
                            var sapPath = $"sound/{soundFileName}";
                            var dstPath = fileRepository.GetModPath(sapPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(dstPath));

                            logger.WriteLine($"  Setting {soundFileName} to {file}");
                            soundProcessActions.Add(() =>
                            {
                                var waveformBuilder = new WaveformBuilder();
                                waveformBuilder.Append(file);
                                lock (sapLock)
                                    waveformBuilder.Save(dstPath);
                            });
                        }
                    }
                }
            }

            // Do sound processing in bulk / parallel
            Parallel.ForEach(soundProcessActions, x => x());
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig, double volume)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".wav", volume);
        }

        protected override void SerialiseInventory(FileRepository fileRepository)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Init");
            var player = 0;
            foreach (var inventory in Inventories.Reverse<RandomInventory?>())
            {
                var playerNode = doc.CreateElement("Player");
                if (inventory != null)
                {
                    var maxItems = player == 0 ? 6 + 6 : 8;
                    var entries = inventory.Entries;
                    for (int i = 0; i < maxItems; i++)
                    {
                        var entry = entries.Length > i ? entries[i] : new RandomInventory.Entry();
                        var entryNode = doc.CreateElement("Entry");
                        entryNode.SetAttribute("id", entry.Type.ToString());
                        entryNode.SetAttribute("count", entry.Count.ToString());
                        playerNode.AppendChild(entryNode);
                    }
                }
                root.AppendChild(playerNode);
                player++;
            }
            doc.AppendChild(root);
            doc.Save(fileRepository.GetModPath("init.xml"));
        }

        internal override string BGMPath => "sound";

        internal static string FindDataPath(string installPath)
        {
            var originalDataPath = Path.Combine(installPath, "JPN");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = Path.Combine(installPath, "USA");
                if (!Directory.Exists(originalDataPath))
                {
                    originalDataPath = installPath;
                }
            }
            return originalDataPath;
        }

        private void FixWeaponHitScan(RandoConfig config)
        {
            var table = new short[]
            {
                -2026, -1656, -2530, -2280, -2040, -1800,
                -1917, -1617, -2190, -1940, -2003, -1720
            };

            var numValues = table.Length / 2;
            if (config.SwapCharacters)
            {
                // Swap Chris and Jills values around
                for (var i = 0; i < numValues; i++)
                {
                    (table[i], table[i + numValues]) = (table[i + numValues], table[i]);
                }
            }

            for (var i = 0; i < 2; i++)
            {
                var pldPath = GetSelectedPldPath(config, i);
                var csvPath = Path.Combine(pldPath, "weapons.csv");
                if (!File.Exists(csvPath))
                    continue;

                var csv = File.ReadAllLines(csvPath)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().Split(','))
                    .ToArray();

                var offset = i * numValues;
                for (var j = 0; j < numValues; j++)
                {
                    table[offset + j] = short.Parse(csv[j][0]);
                }
            }

            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x4AAD98);
            var data = MemoryMarshal.Cast<short, byte>(new Span<short>(table));
            foreach (var d in data)
            {
                pw.Write(d);
            }
            pw.End();
        }
    }
}
