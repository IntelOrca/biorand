using System.IO;
using Xunit;

namespace IntelOrca.Biohazard.Tests
{
    public class TestRE3Archive
    {
        [Fact]
        public void GetFileContents_R100()
        {
            var installPath = TestInfo.GetInstallPath(2);
            var rofs13Path = Path.Combine(installPath, "rofs13.dat");
            var rofs13 = new RE3Archive(rofs13Path);
            var rdt100 = rofs13.GetFileContents("DATA_J/RDT/R100.RDT");
            var fnv1a = rdt100.CalculateFnv1a();
            Assert.Equal(0x1AA70, rdt100.Length);
            Assert.Equal(0xD22BEF93F3D0442B, fnv1a);
        }

        [Fact]
        public void GetFileContents_R10D()
        {
            var installPath = TestInfo.GetInstallPath(2);
            var rofs13Path = Path.Combine(installPath, "rofs13.dat");
            var rofs13 = new RE3Archive(rofs13Path);
            var rdt10D = rofs13.GetFileContents(13);
            var fnv1a = rdt10D.CalculateFnv1a();
            Assert.Equal(0x27CB0, rdt10D.Length);
            Assert.Equal(0xDCA4BA45867EEAAD, fnv1a);
        }
    }
}
