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
        protected override BioVersion BiohazardVersion => BioVersion.Biohazard2;
        internal override IItemHelper ItemHelper { get; } = new Re2ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re2EnemyHelper();

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
            var checksumsForEachPlayer = JsonSerializer.Deserialize<Dictionary<string, ulong>[]>(Resources.checksum)!;
            var checksums = checksumsForEachPlayer[player];
            return checksums.ToDictionary(x => RdtId.Parse(x.Key), x => x.Value);
        }

        protected override void Generate(RandoConfig config, ReInstallConfig reConfig, string installPath, string modPath)
        {
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
                var bgmRandomiser = new BgmRandomiser(logger, bgmDirectory, GetBgmJson(), false, new Rng(config.Seed));
                AddMusicSelection(bgmRandomiser, reConfig);
                if (reConfig.IsEnabled(BioVersion.Biohazard1))
                {
                    var re1r = new Re1Randomiser();
                    re1r.AddMusicSelection(bgmRandomiser, reConfig);
                }
                bgmRandomiser.Randomise();
            }

            RandoBgCreator.Save(config, modPath, BiohazardVersion);
        }

        public void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BioVersion.Biohazard2));
            var srcBgmDirectory = Path.Combine(dataPath, @"Common\Sound\BGM");
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".sap");
        }

        protected override string GetJsonMap()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\rdt.json");
            var jsonMap = File.ReadAllText(jsonPath);
#else
            var jsonMap = Resources.rdt;
#endif
            return jsonMap;
        }

        private static string GetBgmJson()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\bgm.json");
            return File.ReadAllText(jsonPath);
#else
            return Resources.bgm;
#endif
        }
    }
}
