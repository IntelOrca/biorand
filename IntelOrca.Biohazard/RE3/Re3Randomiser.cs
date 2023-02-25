using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.RE3
{
    public class Re3Randomiser : BaseRandomiser
    {
        private ReInstallConfig? _reInstallConfig;

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard3;
        internal override IItemHelper ItemHelper => new Re3ItemHelper();
        internal override IEnemyHelper EnemyHelper => new Re3EnemyHelper();
        internal override INpcHelper NpcHelper => new Re3NpcHelper();
        internal override string BGMPath => "DATA_A/SOUND";

        public Re3Randomiser(IBgCreator? bgCreator)
            : base(bgCreator)
        {
        }

        public override string GetPlayerName(int player) => "Jill";

        internal FileRepository CreateRepository(string installPath, string? modPath = null)
        {
            var repo = new FileRepository(installPath, null);
            AddArchives(installPath, repo);
            return repo;
        }

        internal void AddArchives(string installPath, FileRepository fileRepo)
        {
            var dataPath = GetDataPath(installPath);
            foreach (var path in Directory.GetFiles(dataPath, "rofs*.dat"))
            {
                fileRepo.AddRE3Archive(path);
            }
        }

        public override bool ValidateGamePath(string path)
        {
            return File.Exists(Path.Combine(path, "rofs1.dat"));
        }

        protected override string GetDataPath(string installPath)
        {
            return installPath;
        }

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            var repo = CreateRepository(dataPath);
            var files = repo.GetFiles(Path.Combine(dataPath, "DATA_J", "RDT"));
            var rdts = files
                .Where(x => x.EndsWith(".RDT", StringComparison.OrdinalIgnoreCase))
                .Select(x => RdtId.Parse(x.Substring(x.Length - 7, 3)))
                .Where(x => x.Stage <= 4)
                .ToArray();
            return rdts;
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player)
        {
            return Path.Combine(dataPath, "DATA_J", "RDT", $"R{rdtId}.RDT");
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".WAV");
        }

        public override void Generate(RandoConfig config, ReInstallConfig reConfig, IRandoProgress progress, FileRepository fileRepository)
        {
            _reInstallConfig = reConfig;
            AddArchives(fileRepository.DataPath, fileRepository);

            if (config.RandomBgm)
            {
                if (MusicAlbumSelected("RE1") && !reConfig.IsEnabled(BioVersion.Biohazard1))
                {
                    throw new BioRandUserException("RE1 installation must be enabled to use RE1 assets.");
                }
                if (MusicAlbumSelected("RE2") && !reConfig.IsEnabled(BioVersion.Biohazard2))
                {
                    throw new BioRandUserException("RE2 installation must be enabled to use RE2 assets.");
                }
            }
            if (!reConfig.IsEnabled(BioVersion.Biohazard3))
            {
                throw new BioRandUserException("RE3 installation must be enabled to randomize RE3.");
            }

            GenerateRdts(config, progress, fileRepository);

            base.Generate(config, reConfig, progress, fileRepository);
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

        protected override string[] GetDefaultNPCs()
        {
            return new[] { "jill.re3", "brad", "mikhail", "nikolai", "dario", "murphy", "tyrell", "carlos",
                "marvin", "irons" };
        }

        internal override string? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            var actor = "jill.re3";
            if (config.ChangePlayer)
            {
                var pldIndex = config.Player == 0 ? config.Player0 : config.Player1;
                var pldPath = DataManager.GetDirectories(BiohazardVersion, $"pld{config.Player}")
                    .Skip(pldIndex)
                    .FirstOrDefault();
                actor = Path.GetFileName(pldPath);
                SwapPlayerCharacter(config, logger, actor, fileRepository);
            }
            return actor;
        }

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, string actor, FileRepository fileRepository)
        {
            var originalPlayerActor = "jill.re3";
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld0\\{actor}");

            if (originalPlayerActor != actor)
            {
                logger.WriteHeading("Randomizing Player:");
                logger.WriteLine($"{originalPlayerActor} becomes {actor}");
            }

            var targetPldDir = fileRepository.GetModPath($"DATA/PLD");
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
                var numbers = new[] { 1, 2, 3, 4, 5, 6, 7 };
                var src = Path.Combine(targetPldDir, $"pl{config.Player:X2}.pld");
                foreach (var n in numbers)
                {
                    var dst = Path.Combine(targetPldDir, $"pl{n:X2}.pld");
                    File.Copy(src, dst, true);
                }

                ChangePlayerInventoryFace(config, actor, fileRepository);

                // var allHurtFiles = DataManager.GetHurtFiles(actor)
                //     .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                //     .ToArray();
                // var hurtFiles = new string[4];
                // foreach (var hurtFile in allHurtFiles)
                // {
                //     if (int.TryParse(Path.GetFileNameWithoutExtension(hurtFile), out var i))
                //     {
                //         if (i < hurtFiles.Length)
                //         {
                //             hurtFiles[i] = hurtFile;
                //         }
                //     }
                // }
                // if (hurtFiles.All(x => x != null))
                // {
                //     var corePath = fileRepository.GetModPath($"common/sound/core/core{config.Player:X2}.sap");
                //     Directory.CreateDirectory(Path.GetDirectoryName(corePath));
                //     for (int i = 0; i < hurtFiles.Length; i++)
                //     {
                //         var waveformBuilder = new WaveformBuilder();
                //         waveformBuilder.Append(hurtFiles[i]);
                //         if (i == 0)
                //             waveformBuilder.Save(corePath, 0x0F);
                //         else
                //             waveformBuilder.SaveAppend(corePath);
                //     }
                //     {
                //         var coreDeathPath = fileRepository.GetModPath($"common/sound/core/core{32 + config.Player:00}.sap");
                //         var waveformBuilder = new WaveformBuilder();
                //         waveformBuilder.Append(hurtFiles[3]);
                //         waveformBuilder.Save(coreDeathPath);
                //     }
                // }
            }
        }

        private void ChangePlayerInventoryFace(RandoConfig config, string actor, FileRepository fileRepository)
        {
            if (BgCreator == null)
                return;

            var facePath = DataManager.GetPath(BiohazardVersion, Path.Combine($"pld{config.Player}", actor, "face.png"));
            if (!File.Exists(facePath))
                return;

            var filename = Path.Combine("DATA_J", "ETC2", "STMAIN0J.TIM");
            var inputTimPath = fileRepository.GetDataPath(filename);
            var outputTimPath = fileRepository.GetModPath(filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

            var tim = new TimFile(fileRepository.GetStream(inputTimPath));
            BgCreator.DrawImage(tim, facePath, 0, 192, 2);
            tim.Save(outputTimPath);
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser)
        {
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard1))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard1));
                dataPath = Path.Combine(dataPath, "JPN");
                npcRandomiser.AddToSelection(BioVersion.Biohazard1, new FileRepository(dataPath));
            }
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard2))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard2));
                // HACK should be helper function from RE 2 randomizer
                if (Directory.Exists(Path.Combine(dataPath, "data", "pl0", "rdt")))
                {
                    dataPath = Path.Combine(dataPath, "data");
                }
                npcRandomiser.AddToSelection(BioVersion.Biohazard2, new FileRepository(dataPath));
            }
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard3))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard3));
                var fileRepository = new FileRepository(dataPath);
                var re3randomizer = new Re3Randomiser(null);
                re3randomizer.AddArchives(dataPath, fileRepository);
                npcRandomiser.AddToSelection(BioVersion.Biohazard3, fileRepository);
            }
        }

        protected override void SerialiseInventory(FileRepository fileRepository)
        {
            const uint AddressInventoryStart = 0x0052D20C;
            // const uint AddressRefHard = 0x004609F3;
            const uint AddressRefEasy = 0x004609EB;

            if (Inventories.Count == 0)
                return;

            var inventoryX = Inventories[0];
            if (inventoryX == null)
                return;

            var jillInventory = inventoryX.Entries.Take(8).ToArray();
            var carlosInventory = inventoryX.Entries.Skip(8).Take(8).ToArray();
            var inventories = new[] { jillInventory, carlosInventory };

            var bw = new BinaryWriter(ExePatch);

            // Hard then easy
            var offset = AddressInventoryStart;
            bw.Write(offset);
            var lengthPosition = bw.BaseStream.Position;
            bw.Write(0);

            var baseAddress = bw.BaseStream.Position;
            uint easyAddress = 0;
            for (int j = 0; j < 2; j++)
            {
                if (j == 1)
                    easyAddress = (uint)(offset + bw.BaseStream.Position - baseAddress);

                // Jill then Carlos
                for (int i = 0; i < 2; i++)
                {
                    var inventory = inventories[i];
                    foreach (var entry in inventory)
                    {
                        if (entry.Type == 0)
                            break;

                        bw.Write((byte)entry.Type);
                        bw.Write((byte)entry.Count);
                        bw.Write((byte)GetInventoryQuantityStyle(entry.Type));
                        bw.Write((byte)0);
                    }
                    bw.Write((int)-1);
                }
                bw.Write((int)0);
            }

            // Fix length
            var backupPosition = bw.BaseStream.Position;
            bw.BaseStream.Position = lengthPosition;
            bw.Write((uint)(backupPosition - baseAddress));
            bw.BaseStream.Position = backupPosition;

            // Write easy address location
            bw.Write(AddressRefEasy);
            bw.Write(4);
            bw.Write(easyAddress);
        }

        private byte GetInventoryQuantityStyle(byte type)
        {
            const byte None = 0b00;
            const byte Digit = 0b01;
            const byte Percent = 0b10;
            const byte Infinity = 0b11;
            const byte Green = 0 << 2;
            const byte Red = 1 << 2;
            const byte Orange = 2 << 2;
            const byte Blue = 2 << 2;
            const byte Automatic = 0b10000;

            switch (type)
            {
                case Re3ItemIds.HandgunSigpro:
                case Re3ItemIds.HandgunBeretta:
                case Re3ItemIds.ShotgunBenelli:
                case Re3ItemIds.MagnumSW:
                case Re3ItemIds.GrenadeLauncherGrenade:
                case Re3ItemIds.RocketLauncher:
                case Re3ItemIds.MineThrower:
                case Re3ItemIds.HangunEagle:
                case Re3ItemIds.ShotgunM37:
                case Re3ItemIds.HandgunAmmo:
                case Re3ItemIds.MagnumAmmo:
                case Re3ItemIds.ShotgunAmmo:
                case Re3ItemIds.GrenadeRounds:
                case Re3ItemIds.MineThrowerAmmo:
                case Re3ItemIds.FirstAidSprayBox:
                    return Green | Digit;
                case Re3ItemIds.GrenadeLauncherFlame:
                case Re3ItemIds.HandgunSigproEnhanced:
                case Re3ItemIds.HandgunBerettaEnhanced:
                case Re3ItemIds.ShotgunBenelliEnhanced:
                case Re3ItemIds.MineThrowerEnhanced:
                case Re3ItemIds.FlameRounds:
                case Re3ItemIds.RifleAmmo:
                case Re3ItemIds.HandgunEnhancedAmmo:
                case Re3ItemIds.ShotgunEnhancedAmmo:
                    return Red | Digit;
                case Re3ItemIds.RifleM4A1Manual:
                    return Automatic | Green | Percent;
                case Re3ItemIds.RifleM4A1Auto:
                    return Automatic | Red | Percent;
                case Re3ItemIds.GrenadeLauncherAcid:
                case Re3ItemIds.AcidRounds:
                    return Orange | Digit;
                case Re3ItemIds.GrenadeLauncherFreeze:
                case Re3ItemIds.FreezeRounds:
                    return Blue | Digit;
                case Re3ItemIds.GatlingGun:
                    return Green | Infinity;
                case Re3ItemIds.InkRibbon:
                    return Blue | Digit;
                default:
                    return None;
            }
        }

        protected override void ReplaceTitleCardSound(FileRepository fileRepository, string sourcePath)
        {
            var filename = "DATA/SOUND/C_01.VB";
            var vbSourcePath = fileRepository.GetDataPath(filename);
            var vbSourceFile = fileRepository.GetBytes(vbSourcePath);
            var vbTargetPath = fileRepository.GetModPath(filename);
            Directory.CreateDirectory(Path.GetDirectoryName(vbTargetPath));

            var waveBuilder = new WaveformBuilder(channels: 1);
            waveBuilder.Append(sourcePath);
            var pcmData = waveBuilder.GetPCM();
            waveBuilder.Save(@"M:\temp\resampled.wav");

            var vb = new VabFile(vbSourceFile);
            vb.SetSampleFromPCM(5, pcmData);
            vb.SetSampleFromPCM(6, pcmData);
            vb.Write(vbTargetPath);
        }
    }
}
