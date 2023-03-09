using System;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Script;
using Xunit;
using Xunit.Abstractions;
using static IntelOrca.Biohazard.Script.ScdAssembler;

namespace IntelOrca.Biohazard.Tests
{
    public class TestReassemble
    {
        private readonly ITestOutputHelper _output;

        public TestReassemble(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RE2_Leon()
        {
            CheckRDTs(Path.Combine(TestInfo.GetInstallPath(1), @"data\pl0\rdt"));
        }

        [Fact]
        public void RE2_Claire()
        {
            CheckRDTs(Path.Combine(TestInfo.GetInstallPath(1), @"data\pl1\rdt"));
        }

        [Fact]
        public void RE3()
        {
            var installPath = TestInfo.GetInstallPath(2);
            var rofs = new RE3Archive(Path.Combine(installPath, "rofs13.dat"));
            var fail = false;
            foreach (var file in rofs.Files)
            {
                var fileName = Path.GetFileName(file);
                var rdt = rofs.GetFileContents(file);
                var rdtFile = new RdtFile(rdt, BioVersion.Biohazard3);
                var sPath = Path.ChangeExtension(fileName, ".s");
                fail |= AssertReassembleRdt(rdtFile, sPath);
            }
            Assert.False(fail);
        }

        private void CheckRDTs(string rdtPath)
        {
            var rdts = Directory.GetFiles(rdtPath, "*.rdt");
            var fail = false;
            foreach (var rdt in rdts)
            {
                var rdtId = RdtId.Parse(rdt.Substring(rdt.Length - 8, 3));
                if (rdtId == new RdtId(4, 0x05))
                    continue;
                if (rdtId == new RdtId(6, 0x05))
                    continue;
                if (rdtId.Stage > 6)
                    continue;

                var rdtFile = new RdtFile(rdt);
                var sPath = Path.ChangeExtension(rdt, ".s");
                fail |= AssertReassembleRdt(rdtFile, sPath);
            }
            Assert.False(fail);
        }

        private bool AssertReassembleRdt(RdtFile rdtFile, string sPath)
        {
            var diassembly = rdtFile.DisassembleScd();

            var scdAssembler = new ScdAssembler();
            var err = scdAssembler.Assemble(sPath, diassembly);
            var fail = false;
            if (err != 0)
            {
                foreach (var error in scdAssembler.Errors.Errors)
                {
                    _output.WriteLine(error.ToString());
                }
                fail = true;
            }
            else
            {
                var scdInit = rdtFile.GetScd(BioScriptKind.Init);
                var index = CompareByteArray(scdInit, scdAssembler.OutputInit);
                if (index != -1)
                {
                    _output.WriteLine(".init differs at 0x{0:X2} for '{1}'", index, sPath);
                    fail = true;
                }

                if (rdtFile.Version != BioVersion.Biohazard3)
                {
                    var scdMain = rdtFile.GetScd(BioScriptKind.Main);
                    index = CompareByteArray(scdMain, scdAssembler.OutputMain);
                    if (index != -1)
                    {
                        _output.WriteLine(".main differs at 0x{0:X2} for '{1}'", index, sPath);
                        fail = true;
                    }
                }
            }
            return fail;
        }

        private static int CompareByteArray(byte[] a, byte[] b)
        {
            var minLen = Math.Min(a.Length, b.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] != b[i])
                    return i;
            }
            if (a.Length != b.Length)
                return minLen;
            return -1;
        }
    }
}
