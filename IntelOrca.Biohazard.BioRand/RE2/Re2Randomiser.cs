﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.RE1;
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
        internal override IDoorHelper DoorHelper { get; } = new Re2DoorHelper();
        internal override IItemHelper ItemHelper { get; } = new Re2ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re2EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re2NpcHelper();

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
            FixWeaponHitScan(config);
            DisableWaitForSherry();

            // tmoji.bin
            var src = DataManager.GetPath(BiohazardVersion, "tmoji.bin");
            var dst = fileRepository.GetModPath("common/data/tmoji.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst);

            base.Generate(config, reConfig, progress, fileRepository);

            // If Ben is replaced, they need special morphing patches applied
            FixIronsReplacement(fileRepository.GetModPath("pl1/emd1/em140.emd"));
            FixIronsReplacement(fileRepository.GetModPath("pl1/emd1/em142.emd"));
            FixBenReplacement(fileRepository.GetModPath("pl0/emd0/em044.emd"));
            FixBenReplacement(fileRepository.GetModPath("pl0/emd0/em046.emd"));
        }

        protected override string[] TitleCardSoundFiles { get; } =
            new[] {
                "Common/Sound/core/core16.sap",
                "Common/Sound/core/core17.sap"
            };

        internal override string[] ChangePlayerCharacters(RandoConfig config, RandoLogger logger, GameData gameData, FileRepository fileRepository)
        {
            var actor = config.Player == 0 ? "leon" : "claire";
            var partner = config.Player == 0 ? "ada" : "sherry";

            var pldIndex = config.Player == 0 ? 0 : 1;
            var hurtSoundIndex = config.Player == 0 ? 0 : 1;
            var deathSoundIndex = config.Player == 0 ? 32 : 33;
            if (config.ChangePlayer)
            {
                var pldPath = GetSelectedPldPath(config, config.Player);
                actor = Path.GetFileName(pldPath);
                if (actor.IsSherryActor())
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
            // We still need to replace Leon / Claire so they can use more weapons
            ReplacePlayer(config, logger, fileRepository, pldIndex, hurtSoundIndex, deathSoundIndex, actor);

            // Some characters like Sherry need new enemy animations
            var srcPldDir = GetPldDirectory(actor, out var srcPldIndex);
            var emdFiles = DataManager.GetFiles(BiohazardVersion, Path.Combine(srcPldDir, $"emd{srcPldIndex}"));
            var emdFolder = fileRepository.GetModPath($"pl{config.Player}/emd{config.Player}");
            Directory.CreateDirectory(emdFolder);
            foreach (var src in emdFiles)
            {
                var dstFileName = Path.GetFileName(src);
                var match = Regex.Match(dstFileName, "em[0-9]([0-9a-f][0-9a-f]).emd", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    dstFileName = $"em{config.Player}{match.Groups[1].Value}.emd";
                }
                var dst = Path.Combine(emdFolder, dstFileName);
                File.Copy(src, dst, true);
            }

            // Change partner
            var rng = new Rng(config.Seed + config.Player);
            var enabledPLDs = GetEnabledPartners(config);
            if (enabledPLDs.Length != 0)
            {
                partner = rng.NextOf(enabledPLDs);
                pldIndex = config.Player == 0 ? 0x0E : 0x0F;
                hurtSoundIndex = config.Player == 0 ? 14 : 15;
                deathSoundIndex = config.Player == 0 ? 37 : 38;
                ReplacePlayer(config, logger, fileRepository, pldIndex, hurtSoundIndex, deathSoundIndex, partner);
            }
            return new[] { actor, partner };
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

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser, VoiceRandomiser voiceRandomiser)
        {
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard1))
            {
                var dataPath = Re1Randomiser.FindDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard1));
                voiceRandomiser.AddToSelection(BioVersion.Biohazard1, new FileRepository(dataPath));
            }
            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard3))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard3));
                var fileRepository = new FileRepository(dataPath);
                var re3randomizer = new Re3Randomiser(null);
                re3randomizer.AddArchives(dataPath, fileRepository);
                voiceRandomiser.AddToSelection(BioVersion.Biohazard3, fileRepository);
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

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig, double volume)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".sap", volume);
        }

        internal override string BGMPath => @"Common\Sound\BGM";

        private string[] GetEnabledPartners(RandoConfig config)
        {
            var enabledPLDs = GetAllPLDs().Intersect(GetEnabledNPCs(config)).ToArray();
            return enabledPLDs;
        }

        private string[] GetAllPLDs()
        {
            var pldFiles0 = DataManager
                .GetDirectories(BiohazardVersion, $"pld0")
                .Select(x => Path.GetFileName(x))
                .ToArray();
            var pldFiles1 = DataManager
                .GetDirectories(BiohazardVersion, $"pld1")
                .Select(x => Path.GetFileName(x))
                .ToArray();
            return pldFiles0.Concat(pldFiles1).OrderBy(x => x).ToArray();
        }

        private string[] GetEnabledNPCs(RandoConfig config)
        {
            var result = new List<string>();
            var allNPCs = GetNPCs();
            var enabledNPCs = config.EnabledNPCs;
            for (int i = 0; i < allNPCs.Length; i++)
            {
                if (enabledNPCs.Length > i && enabledNPCs[i])
                {
                    result.Add(allNPCs[i]);
                }
            }
            return result.ToArray();
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
            return new[] { "leon", "claire", "ada", "sherry", "annette", "marvin", "irons", "ben", "kendo" };
        }

        private void ReplacePlayer(RandoConfig config, RandoLogger logger, FileRepository fileRepository,
            int pldIndex, int hurtSoundIndex, int deathSoundIndex, string actor)
        {
            var originalPLDs = new[]
            {
                "leon", "claire", "leon", "claire", "leon", "claire", "leon", "claire", "leon", "claire", "leon",
                "chris", "hunk", "tofu", "ada", "sherry"
            };

            var originalPlayerActor = originalPLDs[pldIndex];
            var srcPldDir = GetPldDirectory(actor, out var basePldIndex);

            logger.WriteHeading($"Randomizing Player PL{pldIndex:X2}:");
            logger.WriteLine($"{originalPlayerActor} becomes {actor}");

            // Create target pld folder
            var targetPldDir = fileRepository.GetModPath($"pl{config.Player}/pld");
            Directory.CreateDirectory(targetPldDir);

            // Copy base PLD files
            var basePldFileName = $"PL{basePldIndex:X2}";
            foreach (var pldPath in Directory.GetFiles(fileRepository.GetDataPath($"pl{config.Player}/pld")))
            {
                var pldFileName = Path.GetFileName(pldPath);
                if (pldFileName.StartsWith(basePldFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var pldFile = $"PL{pldIndex:X2}{pldFileName.Substring(4)}";
                    File.Copy(pldPath, Path.Combine(targetPldDir, pldFile), true);
                }
            }

            // Copy override PLD files
            var pldFiles = Directory.GetFiles(srcPldDir);
            foreach (var pldPath in pldFiles)
            {
                var pldFile = Path.GetFileName(pldPath);
                if (pldFile.Length > 4 &&
                    (pldFile.EndsWith("pld", StringComparison.OrdinalIgnoreCase) ||
                     pldFile.EndsWith("plw", StringComparison.OrdinalIgnoreCase)))
                {
                    pldFile = $"PL{pldIndex:X2}{pldFile.Substring(4)}";
                    var targetFile = Path.Combine(targetPldDir, pldFile);
                    File.Copy(pldPath, targetFile, true);
                    if (pldFile.EndsWith("pld", StringComparison.OrdinalIgnoreCase) && pldIndex == 14)
                    {
                        FixAdaPld(targetFile);
                    }
                }
            }

            // Replace other PLDs
            if (actor != originalPlayerActor)
            {
                if (pldIndex == 0 || pldIndex == 1)
                {
                    var numbers = new[] { 2, 4, 6 };
                    var src = Path.Combine(targetPldDir, $"pl{pldIndex:X2}.pld");
                    foreach (var n in numbers)
                    {
                        var dst = Path.Combine(targetPldDir, $"pl{pldIndex + n:X2}.pld");
                        File.Copy(src, dst, true);
                    }
                }

                var faceIndex = pldIndex >= 14 ? 1 : 0;
                ChangePlayerInventoryFace(config, fileRepository, faceIndex, actor);

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
                    var corePath = fileRepository.GetModPath($"common/sound/core/core{hurtSoundIndex:00}.sap");
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
                        var coreDeathPath = fileRepository.GetModPath($"common/sound/core/core{deathSoundIndex:00}.sap");
                        var waveformBuilder = new WaveformBuilder();
                        waveformBuilder.Append(hurtFiles[3]);
                        waveformBuilder.Save(coreDeathPath);
                    }
                }
            }
        }

        private void FixAdaPld(string path)
        {
            try
            {
                // Open the PLD and insert 160 empty positions at the start of mesh 0
                // this prevents the game from morphing any visible primitives
                const ushort skipPositionCount = 160;

                var pldFileFixed = new PldFile(BioVersion.Biohazard2, path);
                var mesh = ((Md1)pldFileFixed.GetMesh(0)).ToBuilder();
                var part = mesh.Parts[0];
                part.Positions.InsertRange(0, new Md1.Vector[skipPositionCount]);
                for (var i = 0; i < part.Triangles.Count; i++)
                {
                    var t = part.Triangles[i];
                    t.v0 += skipPositionCount;
                    t.v1 += skipPositionCount;
                    t.v2 += skipPositionCount;
                    part.Triangles[i] = t;
                }
                for (var i = 0; i < part.Quads.Count; i++)
                {
                    var t = part.Quads[i];
                    t.v0 += skipPositionCount;
                    t.v1 += skipPositionCount;
                    t.v2 += skipPositionCount;
                    t.v3 += skipPositionCount;
                    part.Quads[i] = t;
                }
                pldFileFixed.SetMesh(0, mesh.ToMesh());
                pldFileFixed.Save(path);
            }
            catch
            {
            }
        }

        private void FixBenReplacement(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var emdFile = new EmdFile(BioVersion.Biohazard2, path);

                // Ensure there are 16 meshes (the last one is empty for the G-embryo as it exits the body)
                var mesh = (Md1)emdFile.GetMesh(0);
                EnsureMeshHasCorrectParts(ref mesh, false);
                if (EnsureChestOnPage(ref mesh, 1))
                {
                    mesh = (Md1)mesh.EditMeshTextures(m =>
                    {
                        if (m.PartIndex == 0 || m.PartIndex == 9 || m.PartIndex == 12)
                        {
                            m.Page = 0;
                        }
                    });
                    SwapPagesOnTexture(path);
                }
                emdFile.SetMesh(0, mesh);

                // Create the morph data that Ben needs
                var morphData = new MorphData.Builder();
                morphData.Unknown00 = 0x00008001;

                // Copy skeleton from EMR
                var hunkEmr = emdFile.GetEmr(0);
                var skel = new Emr.Vector[15];
                for (var i = 0; i < 15; i++)
                {
                    skel[i] = hunkEmr.GetRelativePosition(i);
                }
                morphData.Skeletons.Add(skel);
                morphData.Skeletons.Add(skel);

                // Create morph group for chest (copy the chest mesh positions)
                var g0 = new MorphData.Builder.MorphGroup();
                g0.Unknown = 0x10000;
                g0.Positions.Add(mesh.ToBuilder().Parts[0].Positions.Select(p => new Emr.Vector(p.x, p.y, p.z)).ToArray());
                g0.Positions.Add(g0.Positions[0]);
                morphData.Groups.Add(g0);

                // Create morph group for G-embryo
                var g1 = new MorphData.Builder.MorphGroup();
                g1.Unknown = 0x10000;
                g1.Positions.Add(new Emr.Vector[1]);
                g1.Positions.Add(new Emr.Vector[1]);
                morphData.Groups.Add(g1);

                emdFile.SetChunk(0, morphData.ToMorphData());
                emdFile.Save(path);
            }
            catch
            {
            }
        }

        private void FixIronsReplacement(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var emdFile = new EmdFile(BioVersion.Biohazard2, path);

                // Ensure there are 17 meshes (the second to last one is gun hand,
                //                             the last one is empty for the G-embryo as it exits the body)
                var mesh = (Md1)emdFile.GetMesh(0);
                EnsureMeshHasCorrectParts(ref mesh, true);
                if (EnsureChestOnPage(ref mesh, 0))
                {
                    mesh = (Md1)mesh.EditMeshTextures(m =>
                    {
                        if (m.PartIndex == 0 || m.PartIndex == 9 || m.PartIndex == 12)
                        {
                            m.Page = 1;
                        }
                    });
                    SwapPagesOnTexture(path);
                }
                emdFile.SetMesh(0, mesh);

                // Create the morph data that Irons needs
                var morphData = new MorphData.Builder();
                morphData.Unknown00 = 0x00010001;

                // Copy skeleton from EMR
                var hunkEmr = emdFile.GetEmr(0);
                var skel = new Emr.Vector[15];
                for (var i = 0; i < 15; i++)
                {
                    skel[i] = hunkEmr.GetRelativePosition(i);
                }
                morphData.Skeletons.Add(skel);
                morphData.Skeletons.Add(skel);

                // Create morph group for chest (copy the chest mesh positions)
                var g0 = new MorphData.Builder.MorphGroup();
                g0.Unknown = 0x10000;
                g0.Positions.Add(mesh.ToBuilder().Parts[0].Positions.Select(p => new Emr.Vector(p.x, p.y, p.z)).ToArray());
                g0.Positions.Add(g0.Positions[0]);
                g0.Positions.Add(g0.Positions[0]);
                morphData.Groups.Add(g0);

                // Create morph group for G-embryo
                var g1 = new MorphData.Builder.MorphGroup();
                g1.Unknown = 2;
                for (var i = 0; i < 301; i++)
                    g1.Positions.Add(new Emr.Vector[1]);
                morphData.Groups.Add(g1);

                emdFile.SetChunk(0, morphData.ToMorphData());
                emdFile.Save(path);
            }
            catch
            {
            }
        }

        private void EnsureMeshHasCorrectParts(ref Md1 mesh, bool hasHand)
        {
            var builder = mesh.ToBuilder();
            while (builder.Count > 15)
            {
                builder.RemoveAt(builder.Count - 1);
            }
            if (hasHand)
            {
                builder.Add(builder.Parts[11]);
            }
            builder.Add();
            mesh = builder.ToMesh();
        }

        private bool EnsureChestOnPage(ref Md1 mesh, int page)
        {
            var builder = mesh.ToBuilder();
            var part0 = builder.Parts[0];
            if (part0.TriangleTextures.Count > 0)
            {
                if ((part0.TriangleTextures[0].page & 0x0F) == page)
                {
                    return false;
                }
            }
            else if (part0.QuadTextures.Count > 0)
            {
                if ((part0.QuadTextures[0].page & 0x0F) == page)
                {
                    return false;
                }
            }
            mesh = (Md1)mesh.SwapPages(0, 1);
            return true;
        }

        private void SwapPagesOnTexture(string emdPath)
        {
            var timPath = Path.ChangeExtension(emdPath, ".tim");
            if (File.Exists(timPath))
            {
                var tim = new TimFile(timPath);
                tim.SwapPages(0, 1);
                tim.Save(timPath);
            }
        }

        private void ChangePlayerInventoryFace(RandoConfig config, FileRepository fileRepository, int index, string actor)
        {
            if (BgCreator == null)
                return;

            var srcPldDir = GetPldDirectory(actor, out _);
            var facePath = DataManager.GetPath(BiohazardVersion, Path.Combine(srcPldDir, "face.png"));
            if (!File.Exists(facePath))
                return;

            var filename = Path.Combine("common", "data", $"st{config.Player}_jp.tim");
            var inputTimPath = fileRepository.GetDataPath(filename);
            var outputTimPath = fileRepository.GetModPath(filename);
            if (File.Exists(outputTimPath))
                inputTimPath = outputTimPath;
            Directory.CreateDirectory(Path.GetDirectoryName(outputTimPath!));

            var timCollection = new TimCollectionFile(inputTimPath);
            if (timCollection.Tims.Count > 1)
            {
                var tim1 = timCollection.Tims[1];
                if (File.Exists(facePath))
                {
                    BgCreator.DrawImage(tim1, facePath, index * 44, 72);
                }
                timCollection.Save(outputTimPath);
            }
        }

        private string GetPldDirectory(string actor, out int pldIndex)
        {
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld0\\{actor}");
            pldIndex = 0;
            if (!Directory.Exists(srcPldDir))
            {
                srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld1\\{actor}");
                pldIndex = 1;
            }
            return srcPldDir;
        }

        protected override void SerialiseInventory(FileRepository fileRepository)
        {
            var inventoryAddress = new[]
            {
                AddressInventoryLeon,
                AddressInventoryClaire
            };
            var specialAddresses = new[]
            {
                new uint[] { 0x400000 + 0x00102248, 0x400000 + 0x00101C84 },
                new uint[] { 0x400000 + 0x0010223F },
            };

            var bw = new PatchWriter(ExePatch);
            for (int i = 0; i < inventoryAddress.Length; i++)
            {
                if (Inventories.Count <= i)
                    break;

                var inventory = Inventories[i];
                if (inventory == null)
                    continue;

                var offset = inventoryAddress[i];
                bw.Begin(offset);
                for (int j = 0; j < 11; j++)
                {
                    var entry = new RandomInventory.Entry();
                    if (j < inventory.Entries.Length)
                        entry = inventory.Entries[j];
                    else if (j == 10 && inventory.Special.HasValue)
                        entry = inventory.Special.Value;

                    bw.Write((byte)entry.Type);
                    bw.Write((byte)entry.Count);
                    bw.Write((byte)entry.Part);
                }
                bw.End();

                if (inventory.Special is RandomInventory.Entry specialEntry)
                {
                    foreach (var address in specialAddresses[i])
                    {
                        bw.Begin(address);
                        bw.Write(specialEntry.Type);
                        bw.End();
                    }

                    // Set weapon type to none when switching back from partner
                    bw.Begin(0x400000 + 0x1021FF);
                    bw.Write((byte)0xB1);
                    bw.Write((byte)0x80);
                    bw.Write((byte)0x90);
                    bw.End();
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

        private void FixWeaponHitScan(RandoConfig config)
        {
            var table = new short[]
            {
                0, 0, 0, 0,
                -200, 300, 0, 0,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
                -500, 150, -1813, -1506,
                -500, 200, -1813, -1506,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -400, 30, -1850, -1890,
                -600, 600, -1894, -1809,
                -400, 200, -1894, -1809,
                -200, 300, 0, 0,
                -200, 300, 0, 0,
                -200, 300, -2427, -2120,
                -200, 300, -2427, -2120,
            };

            if (config.SwapCharacters)
            {
                // Swap Leon and Claires values around
                for (var i = 0; i < table.Length; i += 4)
                {
                    (table[i + 3], table[i + 2]) = (table[i + 2], table[i + 3]);
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

                var k = 0;
                for (var j = 0; j < table.Length; j += 4)
                {
                    for (var l = 0; l < 4; l++)
                    {
                        if ((i == 0 && l == 3) || (i == 1 && l == 2))
                            continue;

                        table[j + l] = short.Parse(csv[k][l]);
                    }
                    k++;
                }
            }

            var pw = new PatchWriter(ExePatch);
            pw.Begin(0x53A358);
            var data = MemoryMarshal.Cast<short, byte>(new Span<short>(table));
            foreach (var d in data)
            {
                pw.Write(d);
            }
            pw.End();
        }

        private void DisableWaitForSherry()
        {
            // Disable (you must wait for Sherry)
            var bw = new PatchWriter(ExePatch);
            bw.Begin(0x400000 + 0xE9490);
            bw.Write((byte)0xEB);
            bw.End();
        }
    }
}
