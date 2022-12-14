using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using IntelOrca.Biohazard.RE1;

namespace IntelOrca.Biohazard.RE2
{
    public class Re2Randomiser : BaseRandomiser
    {
        private ReInstallConfig? _reInstallConfig;

        protected override BioVersion BiohazardVersion => BioVersion.Biohazard2;
        internal override IItemHelper ItemHelper { get; } = new Re2ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re2EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re2NpcHelper();

        protected override string GetPlayerName(int player) => player == 0 ? "Leon" : "Claire";

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
                .OrderBy(x => x.Stage)
                .ThenBy(x => x.Room)
                .ToArray();
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player)
        {
            var path = Path.Combine(dataPath, @$"Pl{player}\Rdt\ROOM{rdtId}{player}.RDT");
            return path;
        }

        protected override Dictionary<RdtId, ulong> GetRdtChecksums(int player)
        {
            var checksumJson = DataManager.GetData(BiohazardVersion, "checksum.json");
            var checksumsForEachPlayer = JsonSerializer.Deserialize<Dictionary<string, ulong>[]>(checksumJson)!;
            var checksums = checksumsForEachPlayer[player];
            return checksums.ToDictionary(x => RdtId.Parse(x.Key), x => x.Value);
        }

        protected override void Generate(RandoConfig config, ReInstallConfig reConfig, string installPath, string modPath)
        {
            _reInstallConfig = reConfig;

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

            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading("Resident Evil Randomizer");
                logger.WriteLine($"Seed: {config}");

                var bgmDirectory = Path.Combine(modPath, @"Common\Sound\BGM");
                var bgmRandomiser = new BgmRandomiser(logger, bgmDirectory, GetBgmJson(), false, new Rng(config.Seed), DataManager);
                AddMusicSelection(bgmRandomiser, reConfig);
                if (reConfig.IsEnabled(BioVersion.Biohazard1))
                {
                    var re1r = new Re1Randomiser();
                    re1r.AddMusicSelection(bgmRandomiser, reConfig);
                }
                bgmRandomiser.Randomise();
            }

            RandoBgCreator.Save(config, modPath, BiohazardVersion, DataManager);
        }

        internal override void RandomizeNPCs(RandoConfig config, NPCRandomiser npcRandomiser)
        {
            if (_reInstallConfig?.IsEnabled(BioVersion.Biohazard1) == true)
            {
                var dataPath = GetDataPath(_reInstallConfig.GetInstallPath(BioVersion.Biohazard1));
                dataPath = Path.Combine(dataPath, "JPN");
                npcRandomiser.AddToSelection(BioVersion.Biohazard1, dataPath);
                var pldFiles = DataManager.GetFiles(BiohazardVersion, $"pld{config.Player}");
                foreach (var pldPath in pldFiles)
                {
                    var actor = Path.GetFileNameWithoutExtension(pldPath);
                    var facePath = DataManager.GetPath(BiohazardVersion, "face\\" + actor + ".tim");
                    npcRandomiser.AddPC(config.Player == 1, actor, pldPath, facePath);
                }

                for (int i = 0;i < 2; i++)
                {
                    var emdFiles = DataManager.GetFiles(BiohazardVersion, $"emd{i}");
                    foreach (var emdPath in emdFiles)
                    {
                        if (emdPath.EndsWith(".emd", System.StringComparison.OrdinalIgnoreCase))
                        {
                            var actor = Path.GetFileNameWithoutExtension(emdPath);
                            var timPath = Path.ChangeExtension(emdPath, ".tim");
                            npcRandomiser.AddNPC(i == 1, actor, emdPath, timPath);
                        }
                    }
                }
            }
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BioVersion.Biohazard2));
            var srcBgmDirectory = Path.Combine(dataPath, @"Common\Sound\BGM");
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".sap");
        }
    }
}
