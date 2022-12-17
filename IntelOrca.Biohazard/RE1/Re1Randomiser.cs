using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IntelOrca.Biohazard.RE2;

namespace IntelOrca.Biohazard.RE1
{
    public class Re1Randomiser : BaseRandomiser
    {
        protected override BioVersion BiohazardVersion => BioVersion.Biohazard1;
        internal override IItemHelper ItemHelper { get; } = new Re1ItemHelper();
        internal override IEnemyHelper EnemyHelper { get; } = new Re1EnemyHelper();
        internal override INpcHelper NpcHelper { get; } = new Re1NpcHelper();

        public override string GetPlayerName(int player) => player == 0 ? "Chris" : "Jill";

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
            if (config.RandomBgm && config.IncludeBGMRE2)
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

            if (config.RandomItems)
            {
                SerialiseInventory(modPath);
            }

            base.Generate(config, reConfig, installPath, modPath);
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".wav");
        }

        internal override string BGMPath => "sound";
    }
}
