using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using Ps2IsoTools.UDF;
using Ps2IsoTools.UDF.Files;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    public class ReCvRandomiser : BaseRandomiser
    {
        private AfsFile? _rdxAfs;
        private RandomizedRdt[] _rdts = new RandomizedRdt[204];

        protected override BioVersion BiohazardVersion => BioVersion.BiohazardCv;
        internal override IDoorHelper DoorHelper { get; } = new ReCvDoorHelper();
        internal override IItemHelper ItemHelper { get; } = new ReCvItemHelper();
        internal override IEnemyHelper EnemyHelper => new ReCvEnemyHelper();
        internal override INpcHelper NpcHelper { get; } = null;
        internal override string BGMPath => throw new NotImplementedException();

        public ReCvRandomiser(ReInstallConfig installConfig, IBgCreator? bgCreator)
            : base(installConfig, bgCreator)
        {
        }

        protected override RdtId[] GetRdtIds(string dataPath)
        {
            return _rdxLnkRdtIdOrder
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(RdtId.Parse)
                .ToArray();
        }

        private int GetRdxFileIndex(RdtId id)
        {
            for (var i = 0; i < _rdxLnkRdtIdOrder.Length; i++)
            {
                var szRdtId = _rdxLnkRdtIdOrder[i];
                if (string.IsNullOrEmpty(szRdtId))
                    continue;

                if (RdtId.Parse(szRdtId) == id)
                    return i;
            }
            return -1;
        }

        protected override string GetDataPath(string installPath)
        {
            return Path.Combine(installPath, "data");
        }

        protected override string GetRdtPath(string dataPath, RdtId rdtId, int player, bool mod)
        {
            return Path.Combine(dataPath, "RDX_LNK.AFS");
        }

        protected override RandomizedRdt ReadRdt(FileRepository fileRepository, RdtId rdtId, string path, string modPath)
        {
            var fileIndex = GetRdxFileIndex(rdtId);
            if (_rdts[fileIndex] != null)
                return _rdts[fileIndex];

            var prs = new PrsFile(_rdxAfs!.GetFileData(fileIndex));
            var rdt = new RdtCv(prs.Uncompressed);
            var rrdt = new RandomizedRdt(rdt, rdtId);
            rrdt.Ast = CreateAst(rdt);
            _rdts[fileIndex] = rrdt;
            return rrdt;
        }

        public override string GetPlayerName(int player) => "Claire";

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

        internal void AddMusicSelection(BgmRandomiser bgmRandomizer, ReInstallConfig reConfig, double volume)
        {
            var dataPath = GetDataPath(reConfig.GetInstallPath(BiohazardVersion));
            var srcBgmDirectory = Path.Combine(dataPath, BGMPath);
            bgmRandomizer.AddToSelection(GetBgmJson(), srcBgmDirectory, ".WAV", volume);
        }

        public override void Generate(RandoConfig config, IRandoProgress progress, FileRepository fileRepository)
        {
            var reConfig = InstallConfig;
            if (config.RandomBgm)
            {
                if (MusicAlbumSelected(config, "RE1") && !reConfig.IsEnabled(BioVersion.Biohazard1))
                {
                    throw new BioRandUserException("RE1 installation must be enabled to use RE1 assets.");
                }
                if (MusicAlbumSelected(config, "RE2") && !reConfig.IsEnabled(BioVersion.Biohazard2))
                {
                    throw new BioRandUserException("RE2 installation must be enabled to use RE2 assets.");
                }
            }
            if (!reConfig.IsEnabled(BioVersion.Biohazard3))
            {
                throw new BioRandUserException("RE3 installation must be enabled to randomize RE3.");
            }

            config.RandomEnemyPlacement = false;
            config.RandomInventory = false;
            config.RandomCutscenes = false;
            config.RandomEvents = false;
            config.RandomBgm = false;

            var isoDirectory = Path.GetDirectoryName(fileRepository.DataPath);
            var input = Path.Combine(isoDirectory, "recvx.iso");
            var output = Path.Combine(isoDirectory, "recvx_biorand.iso");

            UdfEditor? udfEditor = null;
            try
            {
                FileIdentifier? afsFileId;
                using (progress.BeginTask(null, "Reading ISO file"))
                {
                    udfEditor = new Ps2IsoTools.UDF.UdfEditor(input, output);
                    afsFileId = udfEditor.GetFileByName("RDX_LNK.AFS");
                    if (afsFileId == null)
                        throw new BioRandUserException("RDX_LNK.AFS not found in ISO");

                    _rdxAfs = ReadRdxAfs(udfEditor, afsFileId);
                }

#if DEBUG
                var rdxPath = Path.Combine(fileRepository.ModPath, "rdx_lnk");
                Directory.CreateDirectory(rdxPath);
                Parallel.For(0, _rdxAfs.Count, i =>
                {
                    var prs = new PrsFile(_rdxAfs.GetFileData(i));
                    prs.Uncompressed.WriteToFile(Path.Combine(rdxPath, $"{i:000}"));
                });
#endif

                GenerateRdts(config, progress, fileRepository);
                TestEdits();

                // base.Generate(config, progress, fileRepository);

                using (progress.BeginTask(null, "Compressing room files"))
                {
                    _rdxAfs = WriteRdxAfs();
                }

                using (progress.BeginTask(null, "Creating ISO file"))
                {
                    udfEditor.ReplaceFileStream(afsFileId, new MemoryStream(_rdxAfs!.Data.ToArray()));
                    udfEditor.Rebuild(output);
                }
            }
            finally
            {
                udfEditor?.Dispose();
            }
        }

        private void TestEdits()
        {
#if DEBUG
            // Change 101 door to 10A
            var a = (RdtCv)_rdts[1].RdtFile;
            var ab = a.ToBuilder();
            var d = ab.Aots[4];
            d.Room = 10;
            d.ExitId = 0;
            ab.Aots[4] = d;
            a = ab.ToRdt();
            _rdts[1].RdtFile = a;
#endif
        }

        private AfsFile ReadRdxAfs(UdfEditor editor, FileIdentifier fileId)
        {
            var stream = editor.GetFileStream(fileId);
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            return new AfsFile(data);
        }

        private AfsFile WriteRdxAfs()
        {
            if (_rdxAfs == null)
                throw new Exception();

            var builder = _rdxAfs.ToBuilder();
            var parellelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = 32
            };
            Parallel.For(0, _rdts.Length, parellelOptions, i =>
            {
                var rrdt = _rdts[i];
                if (rrdt != null && rrdt.RdtId != new RdtId(0x0, 0x0B))
                {
                    var prs = PrsFile.Compress(rrdt.RdtFile.Data);
                    builder.Replace(i, prs.Data);
                }
            });
            return builder.ToAfsFile();
        }

        protected override string[] GetDefaultNPCs()
        {
            return new[] { "claire", "steve", "chris", "rodrigo", "alfred", "alexia" };
        }

        private ScriptAst? CreateAst(RdtCv rdt)
        {
            var result = new ScriptAst();
            return result;
        }








        private static string[] _rdxLnkRdtIdOrder = new[]
        {
            "100",
            "101",
            "102",
            "", // 102 again
            "103", // 103 with handgun (not right)
            "", // 103 with bullets (not right)
            "104",
            "105",
            "106",
            "107",
            "108", // (10)
            "109",
            "10A",
            "10B",
            "10C",
            "10D",
            "10E",
            "10F",
            "110",
            "200",
            "", // 200 again (20)
            "", // 200 again
            "202",
            "", // HG ammo
            "203",
            "204",
            "205",
            "206",
            "207",
            "208",
            "209", // (30)
            "20A",
            "20B",
            "20C",
            "",
            "",
            "20D",
            "20E",
        };
    }
}
