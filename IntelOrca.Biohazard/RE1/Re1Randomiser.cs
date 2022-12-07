using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.RE2;

namespace IntelOrca.Biohazard.RE1
{
    public class Re1Randomiser : BaseRandomiser
    {
        protected override BioVersion BiohazardVersion => BioVersion.Biohazard1;
        internal override IItemHelper ItemHelper { get; } = new Re1ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re1EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re1NpcHelper();

        protected override string GetPlayerName(int player) => player == 0 ? "Chris" : "Jill";

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

        protected override void Generate(RandoConfig config, ReInstallConfig reConfig, string installPath, string modPath)
        {
            GenerateRdts(config.WithPlayerScenario(0, 0), installPath, modPath);
            GenerateRdts(config.WithPlayerScenario(1, 0), installPath, modPath);

            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading("Resident Evil Randomizer");
                logger.WriteLine($"Seed: {config}");

                var srcBgmDirectory = Path.Combine(installPath, "sound");
                var dstBgmDirectory = Path.Combine(modPath, "sound");
                var bgmRandomiser = new BgmRandomiser(logger, dstBgmDirectory, GetBgmJson(), true, new Rng(config.Seed));
                AddMusicSelection(bgmRandomiser, reConfig);

                if (reConfig.IsEnabled(BioVersion.Biohazard2))
                {
                    var re2r = new Re2Randomiser();
                    re2r.AddMusicSelection(bgmRandomiser, reConfig);
                }

                bgmRandomiser.Randomise();
            }

            RandoBgCreator.Save(config, modPath, BiohazardVersion);
        }

        public void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BioVersion.Biohazard1));
            var srcBgmDirectory = Path.Combine(dataPath, "sound");
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".wav");
        }

        protected override string GetJsonMap()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\re1\re1_rdt.json");
            var jsonMap = File.ReadAllText(jsonPath);
#else
            var jsonMap = Resources.re1_rdt;
#endif
            return jsonMap;
        }

        private static string GetBgmJson()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\re1\re1_bgm.json");
            return File.ReadAllText(jsonPath);
#else
            return Resources.re1_bgm;
#endif
        }
    }
}
