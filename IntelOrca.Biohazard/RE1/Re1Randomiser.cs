using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace IntelOrca.Biohazard.RE1
{
    public class Re1Randomiser : BaseRandomiser
    {
        protected override BioVersion BiohazardVersion => BioVersion.Biohazard1;
        internal override IItemHelper ItemHelper { get; } = new Re1ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re1EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re1NpcHelper();

        public Re1Randomiser(IBgCreator? bgCreator) : base(bgCreator)
        {
        }

        public override string GetPlayerName(int player) => player == 0 ? "Chris" : "Jill";

        public override string[] GetNPCs()
        {
            var actors = new HashSet<string>();
            actors.AddRange(new[] { "chris", "jill", "barry", "rebecca", "wesker", "enrico", "richard" });
            for (int i = 0; i < 2; i++)
            {
                var emds = DataManager
                    .GetDirectories(BiohazardVersion, $"emd")
                    .Select(Path.GetFileName)
                    .ToArray();
                actors.AddRange(emds);
            }
            return actors
                .Select(x => x.ToTitle())
                .OrderBy(x => x)
                .ToArray();
        }

        public override bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "JPN", "STAGE1")) ||
                Directory.Exists(Path.Combine(path, "STAGE1"));
        }

        protected override string GetDataPath(string installPath)
        {
            var originalDataPath = Path.Combine(installPath, "JPN");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = installPath;
            }
            return originalDataPath;
        }

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

        public override void Generate(RandoConfig config, ReInstallConfig reConfig, string installPath, string modPath)
        {
            if (config.RandomBgm && MusicAlbumSelected("RE2"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard2))
                {
                    throw new BioRandUserException("RE2 installation must be enabled to use RE2 assets.");
                }
            }
            if (!reConfig.IsEnabled(BioVersion.Biohazard1))
            {
                throw new BioRandUserException("RE1 installation must be enabled to randomize RE1.");
            }

            var po = new ParallelOptions();
#if DEBUG
            po.MaxDegreeOfParallelism = 1;
#endif
            // Chris / Jill
            Parallel.Invoke(po,
                () => GenerateRdts(config.WithPlayerScenario(0, 0), installPath, modPath),
                () => GenerateRdts(config.WithPlayerScenario(1, 0), installPath, modPath));

            if (config.ChangePlayer)
            {
                ChangePlayerInventoryFace(config, installPath, modPath);
            }

            base.Generate(config, reConfig, installPath, modPath);
        }

        internal override string? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, string modPath)
        {
            string? actor = null;
            if (config.ChangePlayer)
            {
                var pldIndex = config.Player == 0 ? config.Player0 : config.Player1;
                var pldPath = DataManager.GetDirectories(BiohazardVersion, $"pld{config.Player}")
                    .Skip(pldIndex)
                    .FirstOrDefault();
                actor = Path.GetFileName(pldPath);
                SwapPlayerCharacter(config, logger, actor, modPath);
            }
            return actor;
        }

        private void ChangePlayerInventoryFace(RandoConfig config, string installPath, string modPath)
        {
            if (BgCreator == null)
                return;

            var faceDirectory = DataManager.GetPath(BiohazardVersion, "face");

            var inputTimPath = Path.Combine(installPath, "data", "statface.tim");
            var outputTimPath = Path.Combine(modPath, "data", "statface.tim");
            Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

            var timFile = new TimFile(inputTimPath);

            for (int i = 0; i < 2; i++)
            {
                var player = i == 0 ? config.Player0 : config.Player1;
                var actor = GetPlayerCharacters(i)[player].ToLower();
                var facePath = Path.Combine(faceDirectory, $"{actor}.png");
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
                actor = char.ToUpper(actor[0]) + actor.Substring(1);
                result.Add(actor);
            }
            return result.ToArray();
        }

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, string actor, string modPath)
        {
            var originalPlayerActor = config.Player == 0 ? "chris" : "jill";
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld{config.Player}\\{actor}");
            var srcFacePath = DataManager.GetPath(BiohazardVersion, $"face\\{actor}.tim");

            if (originalPlayerActor != actor)
            {
                logger.WriteHeading("Randomizing Player:");
                logger.WriteLine($"{originalPlayerActor} becomes {actor}");
            }

            var targetEnemyDir = Path.Combine(modPath, $"enemy");
            var targetPlayersDir = Path.Combine(modPath, $"players");
            Directory.CreateDirectory(targetEnemyDir);
            Directory.CreateDirectory(targetPlayersDir);
            var pldFiles = Directory.GetFiles(srcPldDir);
            foreach (var pldPath in pldFiles)
            {
                var pldFile = Path.GetFileName(pldPath);
                var dstDir = pldFile.EndsWith(".emd", StringComparison.OrdinalIgnoreCase) ?
                    targetEnemyDir :
                    targetPlayersDir;
                File.Copy(pldPath, Path.Combine(dstDir, pldFile), true);
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
                    var soundDir = Path.Combine(modPath, "sound");
                    Directory.CreateDirectory(soundDir);
                    for (int i = 0; i < hurtFiles.Length; i++)
                    {
                        var soundPath = Path.Combine(soundDir, $"{originalPlayerActor.ToUpperInvariant()}{i + 1:00}.WAV");
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[i]);
                        waveformBuilder.Save(soundPath);
                    }
                }
            }
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser)
        {
            var emdFolders = DataManager.GetDirectories(BiohazardVersion, $"emd");
            foreach (var emdFolder in emdFolders)
            {
                var actor = Path.GetFileName(emdFolder);
                var files = Directory.GetFiles(emdFolder);
                foreach (var file in files)
                {
                    if (file.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                    {
                        var hex = Path.GetFileName(file).Substring(3, 3);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var result))
                        {
                            npcRandomiser.AddNPC1((byte)result, file, actor);
                        }
                    }
                }
            }
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".wav");
        }

        protected override void SerialiseInventory(string modPath)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Init");
            foreach (var inventory in Inventories.Reverse<RandomInventory?>())
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

        internal override string BGMPath => "sound";
    }
}
