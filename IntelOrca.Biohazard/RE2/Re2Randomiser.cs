using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.RE2
{
    public class Re2Randomiser : BaseRandomiser
    {
        private ReInstallConfig? _reInstallConfig;

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard2;
        internal override IItemHelper ItemHelper { get; } = new Re2ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re2EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re2NpcHelper();

        public override string GetPlayerName(int player) => player == 0 ? "Leon" : "Claire";

        public override bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "data", "pl0")) ||
                Directory.Exists(Path.Combine(path, "pl0"));
        }

        protected override string GetDataPath(string installPath)
        {
            var originalDataPath = Path.Combine(installPath, "data");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = installPath;
            }
            return originalDataPath;
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

        public override void Generate(RandoConfig config, ReInstallConfig reConfig, string installPath, string modPath)
        {
            _reInstallConfig = reConfig;

            if ((config.RandomBgm && config.IncludeBGMRE1) || (config.RandomNPCs && config.IncludeNPCRE1))
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

            var po = new ParallelOptions();
#if DEBUG
            po.MaxDegreeOfParallelism = 1;
#endif
            if (config.GameVariant == 0)
            {
                // Leon A / Claire B
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 0), installPath, modPath),
                    () => GenerateRdts(config.WithPlayerScenario(1, 1), installPath, modPath));
            }
            else
            {
                // Leon B / Claire A
                Parallel.Invoke(po,
                    () => GenerateRdts(config.WithPlayerScenario(0, 1), installPath, modPath),
                    () => GenerateRdts(config.WithPlayerScenario(1, 0), installPath, modPath));
            }

            base.Generate(config, reConfig, installPath, modPath);
        }

        internal override string? ChangePlayerCharacters(RandoConfig config, RandoLogger logger, string modPath)
        {
            if (config.ChangePlayer)
            {
                var pldIndex = config.Player == 0 ? config.Player0 : config.Player1;
                var pldPath = DataManager.GetDirectories(BiohazardVersion, $"pld{config.Player}")
                    .Skip(pldIndex)
                    .FirstOrDefault();
                var actor = Path.GetFileName(pldPath);
                SwapPlayerCharacter(config, logger, actor, modPath);
                return actor;
            }
            else
            {
                // We still need to replace Leon / Claire so they can use more weapons
                var actor = config.Player == 0 ? "leon" : "claire";
                SwapPlayerCharacter(config, logger, actor, modPath);
                return actor;
            }
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser)
        {
            var actors = new HashSet<string>();
            actors.AddRange(new[] { "brad", "hunk" });

            if (_reInstallConfig!.IsEnabled(BioVersion.Biohazard1))
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard1));
                dataPath = Path.Combine(dataPath, "JPN");
                npcRandomiser.AddToSelection(BioVersion.Biohazard1, dataPath);
            }

            if (config.IncludeNPCRE1)
            {
                actors.AddRange(new[] { "chris", "enrico", "jill", "rebecca", "richard", "wesker" });
            }

            for (int i = 0; i < 2; i++)
            {
                var emdFiles = DataManager.GetFiles(BiohazardVersion, $"emd{i}");
                foreach (var emdPath in emdFiles)
                {
                    var actor = Path.GetFileNameWithoutExtension(emdPath);
                    if (config.IncludeNPCOther || actors.Contains(actor))
                    {
                        if (emdPath.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                        {
                            var timPath = Path.ChangeExtension(emdPath, ".tim");
                            npcRandomiser.AddNPC(i == 1, actor, emdPath, timPath);
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
                actor = char.ToUpper(actor[0]) + actor.Substring(1);
                result.Add(actor);
            }
            return result.ToArray();
        }

        private void SwapPlayerCharacter(RandoConfig config, RandoLogger logger, string actor, string modPath)
        {
            var originalPlayerActor = config.Player == 0 ? "leon" : "claire";
            var srcPldDir = DataManager.GetPath(BiohazardVersion, $"pld{config.Player}\\{actor}");
            var srcFacePath = DataManager.GetPath(BiohazardVersion, $"face\\{actor}.tim");

            if (originalPlayerActor != actor)
            {
                logger.WriteHeading("Randomizing Player:");
                logger.WriteLine($"{originalPlayerActor} becomes {actor}");
            }

            var targetPldDir = Path.Combine(modPath, $"pl{config.Player}", "pld");
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

                var dstFacePath = Path.Combine(modPath, $"common", "data", $"st{config.Player}_jp.tim");
                Directory.CreateDirectory(Path.GetDirectoryName(dstFacePath));
                File.Copy(srcFacePath, dstFacePath, true);

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
                    var corePath = Path.Combine(modPath, "common", "sound", "core", $"core{config.Player:X2}.sap");
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
                }
            }
        }
    }
}
