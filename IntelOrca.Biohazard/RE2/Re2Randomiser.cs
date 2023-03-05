﻿using IntelOrca.Biohazard.RE1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.RE3;

namespace IntelOrca.Biohazard.RE2
{
    public class Re2Randomiser : BaseRandomiser
    {
        private const double SherryScaleY = 0.735911602209945;
        private const uint AddressInventoryLeon = 0x400000 + 0x001401B8;
        private const uint AddressInventoryClaire = 0x400000 + 0x001401D9;

        private ReInstallConfig? _reInstallConfig;

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard2;
        internal override IItemHelper ItemHelper { get; } = new Re3ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re3EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re3NpcHelper();

        public Re2Randomiser(IBgCreator? bgCreator) : base(bgCreator)
        {
        }

        public override string GetPlayerName(int player) => player == 0 ? "Leon" : "Claire";

        public override bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "data", "pl0", "rdt")) ||
                Directory.Exists(Path.Combine(path, "pl0", "rdt"));
        }

        protected override string GetDataPath(string installPath)
        {
            if (Directory.Exists(Path.Combine(installPath, "data", "pl0", "rdt")))
            {
                return Path.Combine(installPath, "data");
            }
            return installPath;
        }

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            var rdtPaths = new HashSet<RdtId>();
            for (int player = 0; player < 2; player++)
            {
                var files = Directory.GetFiles(Path.Combine(dataPath, @$"Pl{player}\Rdt"));
                foreach (var file in files)
                {
                    // Check the file is an RDT file
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                        !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Ignore RDTs that are not part of the main game
                    if (!char.IsDigit(fileName[4]))
                        continue;

                    if (RdtId.TryParse(fileName.Substring(4, 3), out var rdtId))
                    {
                        rdtPaths.Add(rdtId);
                    }
                }
            }
            return rdtPaths
                .Except(new[] {
                    new RdtId(6, 0x05),
                    new RdtId(6, 0x07)
                })
                .OrderBy(x => x.Stage)
                .ThenBy(x => x.Room)
                .ToArray();
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player)
        {
            var path = Path.Combine(dataPath, @$"Pl{player}\Rdt\ROOM{rdtId}{player}.RDT");
            return path;
        }

        public override void Generate(RandoConfig config, ReInstallConfig reConfig, IRandoProgress progress, FileRepository fileRepository)
        {
            _reInstallConfig = reConfig;

            if (config.RandomBgm && MusicAlbumSelected(config, "RE1"))
            {
                if (!reConfig.IsEnabled(BioVersion.Biohazard1))
                {
                    throw new BioRandUserException("RE1 installation must be enabled to use RE1 assets.");
                }
            }
            if (!reConfig.IsEnabled(BioVersion.Biohazard2))
            {
                throw new BioRandUserException("RE2 installation must be enabled to randomize RE2.");
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
            if (config.GameVariant == 0)
            {
                // Leon A / Claire B
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 0), progress, fileRepository),
                    () => GenerateRdts(config.WithPlayerScenario(1, 1), progress, fileRepository));
            }
            else
            {
                // Leon B / Claire A
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 1), progress, fileRepository),
                    () => GenerateRdts(config.WithPlayerScenario(1, 0), progress, fileRepository));
            }

            FixClaireWeapons();

            // tmoji.bin
            var src = DataManager.GetPath(BiohazardVersion, "tmoji.bin");
            var dst = fileRepository.GetModPath("common/data/tmoji.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst);

            base.Generate(config, reConfig, progress, fileRepository);
        }

        protected override string[] TitleCardSoundFiles { get; } =
            new[] {
                "Common/Sound/core/core16.sap",
                "Common/Sound/core/core17.sap"
            };

        internal override string? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            string actor;
            if (config.ChangePlayer)
            {
                var pldIndex = config.Player == 0 ? config.Player0 : config.Player1;
                var pldPath = DataManager.GetDirectories(BiohazardVersion, $"pld{config.Player}")
                    .Skip(pldIndex)
                    .FirstOrDefault();
                actor = Path.GetFileName(pldPath);
                SwapPlayerCharacter(config, logger, actor, fileRepository);
                if (actor == "sherry")
                {
                    foreach (var rdt in gameData.Rdts)
                    {
                        if (rdt.RdtId == new RdtId(2, 0x06) ||
                            rdt.RdtId == new RdtId(2, 0x07))
                        {
                            continue;
                        }
                        ScaleEmrY(logger, rdt, EmrFlags.Player, false);
                    }
                }
            }
            else
            {
                // We still need to replace Leon / Claire so they can use more weapons
                actor = config.Player == 0 ? "leon" : "claire";
                SwapPlayerCharacter(config, logger, actor, fileRepository);
            }

            var emdFiles = DataManager.GetFiles(BiohazardVersion, $"pld{config.Player}/{actor}/emd{config.Player}");
            var emdFolder = fileRepository.GetModPath($"pl{config.Player}/emd{config.Player}");
            Directory.CreateDirectory(emdFolder);
            foreach (var src in emdFiles)
            {
                var dst = Path.Combine(emdFolder, Path.GetFileName(src));
                File.Copy(src, dst, true);
            }
            return actor;
        }

        internal static void ScaleEmrY(RandoLogger logger, Rdt rdt, EmrFlags flags, bool inverse)
        {
            var scale = inverse ? 1 / SherryScaleY : SherryScaleY;
            var emrIndex = rdt.ScaleEmrY(flags, scale);
            if (emrIndex != null)
            {
                logger.WriteLine($"  {rdt}: EMR [{flags}] Y offsets scaled by {scale}");
            }
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser)
        {
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard1))
            {
                var dataPath = Re1Randomiser.FindDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard1));
                npcRandomiser.AddToSelection(BioVersion.Biohazard1, new FileRepository(dataPath));
            }
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard3))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard3));
                var fileRepository = new FileRepository(dataPath);
                var re3randomizer = new Re3Randomiser(null);
                re3randomizer.AddArchives(dataPath, fileRepository);
                npcRandomiser.AddToSelection(BioVersion.Biohazard3, fileRepository);
            }

            var emdFolders = DataManager.GetDirectories(BiohazardVersion, $"emd");
            foreach (var emdFolder in emdFolders)
            {
                var actor = Path.GetFileName(emdFolder);
                var files = Directory.GetFiles(emdFolder);
                foreach (var file in files)
                {
                    if (file.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                    {
                        var hex = Path.GetFileName(file).Substring(3, 2);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var result))
                        {
                            npcRandomiser.AddNPC((byte)result, file, actor);
                        }
                    }
                }
            }
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".sap");
        }

        internal override string BGMPath => @"Common\Sound\BGM";

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

        protected override string[] GetDefaultNPCs()
        {
            return new[] { "leon", "claire", "ada", "sherry", "annette", "marvin", "irons", "ben", "kendo" };
        }

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, string actor, FileRepository fileRepository)
        {
            var originalPlayerActor = config.Player == 0 ? "leon" : "claire";
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld{config.Player}\\{actor}");

            if (originalPlayerActor != actor)
            {
                logger.WriteHeading("Randomizing Player:");
                logger.WriteLine($"{originalPlayerActor} becomes {actor}");
            }

            var targetPldDir = fileRepository.GetModPath($"pl{config.Player}/pld");
            Directory.CreateDirectory(targetPldDir);
            var pldFiles = Directory.GetFiles(srcPldDir);
            foreach (var pldPath in pldFiles)
            {
                var pldFile = Path.GetFileName(pldPath);
                File.Copy(pldPath, Path.Combine(targetPldDir, pldFile), true);
            }

            // Replace other PLDs
            if (actor != originalPlayerActor)
            {
                var numbers = new[] { 2, 4, 6 };
                var src = Path.Combine(targetPldDir, $"pl{config.Player:X2}.pld");
                foreach (var n in numbers)
                {
                    var dst = Path.Combine(targetPldDir, $"pl{config.Player + n:X2}.pld");
                    File.Copy(src, dst, true);
                }

                ChangePlayerInventoryFace(config, actor, fileRepository);

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
                    var corePath = fileRepository.GetModPath($"common/sound/core/core{config.Player:X2}.sap");
                    Directory.CreateDirectory(Path.GetDirectoryName(corePath));
                    for (int i = 0; i < hurtFiles.Length; i++)
                    {
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[i]);
                        if (i == 0)
                            waveformBuilder.Save(corePath, 0x0F);
                        else
                            waveformBuilder.SaveAppend(corePath);
                    }
                    {
                        var coreDeathPath = fileRepository.GetModPath($"common/sound/core/core{32 + config.Player:00}.sap");
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[3]);
                        waveformBuilder.Save(coreDeathPath);
                    }
                }
            }
        }

        private void ChangePlayerInventoryFace(RandoConfig config, string actor, FileRepository fileRepository)
        {
            if (BgCreator == null)
                return;

            var facePath = DataManager.GetPath(BiohazardVersion, Path.Combine($"pld{config.Player}", actor, "face.png"));
            if (!File.Exists(facePath))
                return;

            var filename = Path.Combine("common", "data", $"st{config.Player}_jp.tim");
            var inputTimPath = fileRepository.GetDataPath(filename);
            var outputTimPath = fileRepository.GetModPath(filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

            var timCollection = new TimCollectionFile(inputTimPath);
            if (timCollection.Tims.Count > 1)
            {
                var tim1 = timCollection.Tims[1];
                if (File.Exists(facePath))
                {
                    BgCreator.DrawImage(tim1, facePath, 0, 72);
                }
                timCollection.Save(outputTimPath);
            }
        }

        protected override void SerialiseInventory(FileRepository fileRepository)
        {
            var inventoryAddress = new[]
            {
                AddressInventoryLeon,
                AddressInventoryClaire
            };

            var bw = new BinaryWriter(ExePatch);
            for (int i = 0; i < inventoryAddress.Length; i++)
            {
                if (Inventories.Count <= i)
                    break;

                var inventory = Inventories[i];
                if (inventory == null)
                    continue;

                var offset = inventoryAddress[i];
                bw.Write(offset);
                bw.Write(inventory.Entries.Length * 3);
                foreach (var entry in inventory.Entries)
                {
                    bw.Write((byte)entry.Type);
                    bw.Write((byte)entry.Count);
                    bw.Write((byte)entry.Part);
                }
            }
        }

        private void FixClaireWeapons()
        {
            var offsets = new (int, byte)[]
            {
                (0x400000 + 0x13A386, 0xB8),
                (0x400000 + 0x13A387, 0xF7),
                (0x400000 + 0x13A38E, 0xB8),
                (0x400000 + 0x13A38F, 0xF7),
                (0x400000 + 0x13A396, 0xEF),
                (0x400000 + 0x13A397, 0xF8),
                (0x400000 + 0x13A39E, 0xEF),
                (0x400000 + 0x13A39F, 0xF8),
                (0x400000 + 0x13A598, 0x60),
                (0x400000 + 0x13A599, 0x64)
            };

            var bw = new BinaryWriter(ExePatch);
            foreach (var (o, b) in offsets)
            {
                bw.Write(o);
                bw.Write(1);
                bw.Write(b);
            }
        }
    }
}
