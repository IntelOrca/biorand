using System;
using System.IO;

namespace IntelOrca.Biohazard.RE3
{
    public class Re3Randomiser : BaseRandomiser
    {
        protected override BioVersion BiohazardVersion => BioVersion.Biohazard3;
        internal override IItemHelper ItemHelper => throw new NotImplementedException();
        internal override IEnemyHelper EnemyHelper => throw new NotImplementedException();
        internal override INpcHelper NpcHelper => throw new NotImplementedException();
        internal override string BGMPath => "DATA_A/SOUND";

        public Re3Randomiser(IBgCreator? bgCreator)
            : base(bgCreator)
        {
        }

        public override string GetPlayerName(int player) => "Jill";

        internal void AddArchives(ReInstallConfig reConfig, FileRepository fileRepo)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
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
            return new RdtId[0];
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player)
        {
            throw new NotImplementedException();
        }

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".WAV");
        }
    }
}
