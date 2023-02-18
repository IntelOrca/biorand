using System;
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

        protected override string[] GetDefaultNPCs()
        {
            return new[] { "jill", "brad", "mikhail", "nikolai", "dario", "murphy", "carlos" };
        }
    }
}
