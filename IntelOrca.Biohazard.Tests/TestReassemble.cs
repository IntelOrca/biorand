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
            CheckRDTs(@"F:\games\re2\data\pl0\rdt");
        }

        [Fact]
        public void RE2_Claire()
        {
            CheckRDTs(@"F:\games\re2\data\pl1\rdt");
        }

        private void CheckRDTs(string rdtPath)
        {
            var rdts = Directory.GetFiles(rdtPath, "*.rdt");
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
                var diassembly = rdtFile.DisassembleScd();
                var sPath = Path.ChangeExtension(rdt, ".s");

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
                        _output.WriteLine(".init differs at 0x{0:X2} for '{1}'", index, rdt);
                        fail = true;
                    }

                    var scdMain = rdtFile.GetScd(BioScriptKind.Main);
                    index = CompareByteArray(scdMain, scdAssembler.OutputMain);
                    if (index != -1)
                    {
                        _output.WriteLine(".main differs at 0x{0:X2} for '{1}'", index, rdt);
                        fail = true;
                    }
                }
                Assert.False(fail);
            }
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
