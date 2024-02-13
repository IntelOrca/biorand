using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using Ps2IsoTools.UDF;
using Ps2IsoTools.UDF.Files;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    public class ReCvRandomiser : BaseRandomiser
    {
        private AfsFile? _rdxAfs;
        private RandomizedRdt[] _rdts = new RandomizedRdt[205];
        private byte[] _elf = new byte[0];

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
            return Enumerable.Range(0, _rdxFileNames.Length)
                .Select(GetRdtId)
                .ToArray();
        }

        private int GetRdxFileIndex(RdtId id)
        {
            for (var i = 0; i < _rdxFileNames.Length; i++)
            {
                var rdtId = GetRdtId(i);
                if (rdtId == id)
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
            var rdtPath = $"R{rdtId}.RDT";
            return mod ?
                Path.Combine(dataPath, "rdx_lnk_modded", rdtPath) :
                Path.Combine(dataPath, "..", "mod_biorand", "rdx_lnk", rdtPath);
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

        protected override RandomizedRdt ReadRdt(FileRepository fileRepository, RdtId rdtId, string path, string modPath)
        {
            var result = base.ReadRdt(fileRepository, rdtId, path, modPath);
            var fileIndex = GetRdxFileIndex(rdtId);
            if (_rdts[fileIndex] == null)
                _rdts[fileIndex] = result;
            return result;
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
                FileIdentifier? elfFileId;
                using (progress.BeginTask(null, "Reading ISO file"))
                {
                    udfEditor = new Ps2IsoTools.UDF.UdfEditor(input, output);
                    afsFileId = udfEditor.GetFileByName("RDX_LNK.AFS");
                    if (afsFileId == null)
                        throw new BioRandUserException("RDX_LNK.AFS not found in ISO");

                    elfFileId = udfEditor.GetFileByName("SLUS_201.84");
                    if (elfFileId == null)
                        throw new BioRandUserException("SLUS_201.84 not found in ISO");

                    _rdxAfs = ReadRdxAfs(udfEditor, afsFileId);
                    _elf = ReadFile(udfEditor, elfFileId);
                }

                // Extract room files
                var rdxPath = Path.Combine(fileRepository.ModPath, "rdx_lnk");
                Directory.CreateDirectory(rdxPath);
                Parallel.For(0, _rdxAfs.Count, i =>
                {
                    var rdtId = GetRdtId(i);
                    var prs = new PrsFile(_rdxAfs.GetFileData(i));
                    prs.Uncompressed.WriteToFile(Path.Combine(rdxPath, $"R{rdtId}.RDT"));
                });

                GenerateRdts(config, progress, fileRepository);

                TestEdits();

                if (InstallConfig.DoorSkip)
                    SetDoorSkip();
                SetInventory();
                SetItemQuantityPickup(config, new Rng(config.Seed));

                // base.Generate(config, progress, fileRepository);

                using (progress.BeginTask(null, "Compressing room files"))
                {
                    _rdxAfs = WriteRdxAfs();
                }

                using (progress.BeginTask(null, "Creating ISO file"))
                {
                    udfEditor.ReplaceFileStream(afsFileId, new MemoryStream(_rdxAfs!.Data.ToArray()));
                    udfEditor.ReplaceFileStream(elfFileId, new MemoryStream(_elf));
                    udfEditor.Rebuild(output);
                }
            }
            finally
            {
                udfEditor?.Dispose();
            }
        }

        private unsafe void TestEdits()
        {
#if DEBUG
            QuickDoor(RdtId.Parse("500"), 0);
#endif
        }

        private void QuickDoor(RdtId destination, int exitId)
        {
            var rdt = (RdtCv)_rdts[1].RdtFile;
            var rdtBuilder = rdt.ToBuilder();
            var aot = rdtBuilder.Aots[4];
            aot.Stage = (byte)destination.Stage;
            aot.Room = (byte)destination.Room;
            aot.ExitId = (byte)exitId;
            rdtBuilder.Aots[4] = aot;
            rdt = rdtBuilder.ToRdt();
            _rdts[1].RdtFile = rdt;
        }

        private ScdProcedureList Replace(ScdProcedureList src, byte[] data)
        {
            var data2 = new byte[src.Data.Length];
            Array.Copy(data, data2, data.Length);
            return new ScdProcedureList(BiohazardVersion, data2);
        }

        private AfsFile ReadRdxAfs(UdfEditor editor, FileIdentifier fileId)
        {
            var data = ReadFile(editor, fileId);
            return new AfsFile(data);
        }

        private byte[] ReadFile(UdfEditor editor, FileIdentifier fileId)
        {
            var stream = editor.GetFileStream(fileId);
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            return data;
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
                if (rrdt != null)
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

        private void SetDoorSkip()
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);
            ms.Position = ConvertAddress(0x133D4C);
            bw.Write(0);
            ms.Position = ConvertAddress(0x133D54);
            bw.Write(0);
        }

        private void SetInventory()
        {
            _elf[ConvertAddress(0x2A6CE8)] = ReCvItemIds.Lighter;
            _elf[ConvertAddress(0x2A6CF0)] = ReCvItemIds.CombatKnife;
        }

        private void SetItemQuantityPickup(RandoConfig config, Rng rng)
        {
            var ms = new MemoryStream(_elf);
            var bw = new BinaryWriter(ms);

            var table = ConvertAddress(0x0035BCC0);
            for (var i = 0; i < 256; i++)
            {
                var itemType = (byte)i;
                var itemAttributes = ItemHelper.GetItemAttributes(itemType);
                if ((itemAttributes & ItemAttribute.InkRibbon) != 0 ||
                    (itemAttributes & ItemAttribute.Ammo) != 0 ||
                    itemType == ReCvItemIds.RocketLauncher)
                {
                    var quantity = GetQuantity(itemType);

                    var offset = table + (i * 4 * 4);
                    for (var j = 0; j < 4; j++)
                    {
                        ms.Position = offset + (j * 4);
                        bw.Write(quantity);
                    }
                }
            }

            int GetQuantity(byte type)
            {
                if (type == ItemHelper.GetItemId(CommonItemKind.InkRibbon))
                {
                    return (byte)rng.Next(1, 3);
                }

                var maxForType = ItemHelper.GetMaxAmmoForAmmoType(type);
                var multiplier = config.AmmoQuantity / 8.0;
                var max = (int)Math.Round(multiplier * maxForType);
                var min = Math.Max(1, max / 2);
                return rng.Next(min, max + 1);
            }
        }

        private static int ConvertAddress(int address)
        {
            return address - 0xFFF80;
        }

        private static RdtId GetRdtId(int fileIndex)
        {
            var fileName = _rdxFileNames[fileIndex];
            var stage = fileName[3] - '0';
            var room = int.Parse(fileName.Substring(4, 2));
            var variant = fileName[6] - '0';
            return new RdtId(stage, room, variant);
        }

        private static string[] _rdxFileNames = new[]
        {
            "RM_0000.RDX",
            "RM_0010.RDX",
            "RM_0020.RDX",
            "RM_0021.RDX",
            "RM_0030.RDX",
            "RM_0031.RDX",
            "RM_0040.RDX",
            "RM_0050.RDX",
            "RM_0060.RDX",
            "RM_0070.RDX",
            "RM_0080.RDX",
            "RM_0090.RDX",
            "RM_0100.RDX",
            "RM_0110.RDX",
            "RM_0120.RDX",
            "RM_0130.RDX",
            "RM_0140.RDX",
            "RM_0150.RDX",
            "RM_0160.RDX",
            "RM_1000.RDX",
            "RM_1001.RDX",
            "RM_1002.RDX",
            "RM_1020.RDX",
            "RM_1021.RDX",
            "RM_1030.RDX",
            "RM_1040.RDX",
            "RM_1050.RDX",
            "RM_1060.RDX",
            "RM_1070.RDX",
            "RM_1080.RDX",
            "RM_1090.RDX",
            "RM_1100.RDX",
            "RM_1110.RDX",
            "RM_1120.RDX",
            "RM_1121.RDX",
            "RM_1122.RDX",
            "RM_1130.RDX",
            "RM_1140.RDX",
            "RM_2000.RDX",
            "RM_2010.RDX",
            "RM_2011.RDX",
            "RM_2020.RDX",
            "RM_2030.RDX",
            "RM_2031.RDX",
            "RM_2040.RDX",
            "RM_2050.RDX",
            "RM_2060.RDX",
            "RM_2070.RDX",
            "RM_3000.RDX",
            "RM_3010.RDX",
            "RM_3011.RDX",
            "RM_3020.RDX",
            "RM_3030.RDX",
            "RM_3040.RDX",
            "RM_3050.RDX",
            "RM_3060.RDX",
            "RM_3070.RDX",
            "RM_3080.RDX",
            "RM_3090.RDX",
            "RM_3091.RDX",
            "RM_3100.RDX",
            "RM_3110.RDX",
            "RM_3120.RDX",
            "RM_3130.RDX",
            "RM_3140.RDX",
            "RM_3150.RDX",
            "RM_3160.RDX",
            "RM_3170.RDX",
            "RM_3180.RDX",
            "RM_3190.RDX",
            "RM_3200.RDX",
            "RM_3210.RDX",
            "RM_3220.RDX",
            "RM_3230.RDX",
            "RM_3240.RDX",
            "RM_4000.RDX",
            "RM_4001.RDX",
            "RM_4010.RDX",
            "RM_4011.RDX",
            "RM_4012.RDX",
            "RM_4020.RDX",
            "RM_4030.RDX",
            "RM_4040.RDX",
            "RM_4050.RDX",
            "RM_5000.RDX",
            "RM_5001.RDX",
            "RM_5010.RDX",
            "RM_5500.RDX",
            "RM_5510.RDX",
            "RM_5520.RDX",
            "RM_5530.RDX",
            "RM_5540.RDX",
            "RM_5550.RDX",
            "RM_5580.RDX",
            "RM_5590.RDX",
            "RM_5600.RDX",
            "RM_5610.RDX",
            "RM_5620.RDX",
            "RM_5630.RDX",
            "RM_5640.RDX",
            "RM_5650.RDX",
            "RM_5660.RDX",
            "RM_5670.RDX",
            "RM_5680.RDX",
            "RM_5690.RDX",
            "RM_5700.RDX",
            "RM_5710.RDX",
            "RM_5800.RDX",
            "RM_5810.RDX",
            "RM_5820.RDX",
            "RM_5830.RDX",
            "RM_5840.RDX",
            "RM_6000.RDX",
            "RM_6010.RDX",
            "RM_6020.RDX",
            "RM_6030.RDX",
            "RM_6040.RDX",
            "RM_6050.RDX",
            "RM_6051.RDX",
            "RM_6060.RDX",
            "RM_6070.RDX",
            "RM_6080.RDX",
            "RM_6090.RDX",
            "RM_6100.RDX",
            "RM_7000.RDX",
            "RM_7010.RDX",
            "RM_7020.RDX",
            "RM_7030.RDX",
            "RM_7031.RDX",
            "RM_7040.RDX",
            "RM_7050.RDX",
            "RM_7060.RDX",
            "RM_7070.RDX",
            "RM_7071.RDX",
            "RM_7080.RDX",
            "RM_7081.RDX",
            "RM_7090.RDX",
            "RM_7100.RDX",
            "RM_7110.RDX",
            "RM_7120.RDX",
            "RM_7130.RDX",
            "RM_7140.RDX",
            "RM_7150.RDX",
            "RM_7160.RDX",
            "RM_7170.RDX",
            "RM_7180.RDX",
            "RM_7181.RDX",
            "RM_7190.RDX",
            "RM_7191.RDX",
            "RM_7200.RDX",
            "RM_7210.RDX",
            "RM_7220.RDX",
            "RM_7221.RDX",
            "RM_7230.RDX",
            "RM_7231.RDX",
            "RM_7240.RDX",
            "RM_7250.RDX",
            "RM_8000.RDX",
            "RM_8001.RDX",
            "RM_8010.RDX",
            "RM_8020.RDX",
            "RM_8030.RDX",
            "RM_8040.RDX",
            "RM_8050.RDX",
            "RM_9000.RDX",
            "RM_9010.RDX",
            "RM_9020.RDX",
            "RM_9030.RDX",
            "RM_9040.RDX",
            "RM_9050.RDX",
            "RM_9060.RDX",
            "RM_9070.RDX",
            "RM_9080.RDX",
            "RM_9090.RDX",
            "RM_9091.RDX",
            "RM_9100.RDX",
            "RM_9110.RDX",
            "RM_9120.RDX",
            "RM_9130.RDX",
            "RM_9140.RDX",
            "RM_9150.RDX",
            "RM_9160.RDX",
            "RM_9170.RDX",
            "RM_9180.RDX",
            "RM_9190.RDX",
            "RM_9200.RDX",
            "RM_9210.RDX",
            "RM_9220.RDX",
            "RM_9230.RDX",
            "RM_9240.RDX",
            "RM_9250.RDX",
            "RM_9260.RDX",
            "RM_9270.RDX",
            "RM_9280.RDX",
            "RM_9290.RDX",
            "RM_9300.RDX",
            "RM_9301.RDX",
            "RM_9302.RDX",
            "RM_9310.RDX",
            "RM_9320.RDX",
            "RM_9321.RDX",
            "RM_9340.RDX",
            "RM_9350.RDX",
            "RM_9360.RDX",
            "RM_9370.RDX",
        };
    }
}
