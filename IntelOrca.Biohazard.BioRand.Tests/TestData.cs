using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace IntelOrca.Biohazard.Tests
{
    public class TestData
    {
        private readonly ITestOutputHelper _output;
        private readonly string _dataPath;

        public TestData(ITestOutputHelper output)
        {
            _output = output;
            _dataPath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "IntelOrca.Biohazard.BioRand", "data");
        }

        [Fact]
        public void TestPLWs()
        {
            var expectedFiles0 = new[] {
                "PL00W03.PLW",
                "PL00W09.PLW",
                "PL00W0A.PLW",
                "PL00W0B.PLW",
                "PL00W0C.PLW",
                "PL00W0D.PLW",
                "PL00W0E.PLW",
                "PL00W13.PLW"
            };
            var expectedFiles1 = new[] {
                "PL01W02.PLW",
                "PL01W04.PLW",
                "PL01W05.PLW",
                "PL01W06.PLW",
                "PL01W07.PLW",
                "PL01W08.PLW",
                "PL01W10.PLW",
                "PL01W13.PLW"
            };

            var result = TestStuff(Path.Combine(_dataPath, "re2", "pld0"), expectedFiles0);
            result &= TestStuff(Path.Combine(_dataPath, "re2", "pld1"), expectedFiles1);
            Assert.True(result, "One or more PLW files are missing");
        }

        private bool TestStuff(string pldPath, string[] expectedFiles)
        {
            var result = true;
            var characters = Directory.GetDirectories(pldPath);
            foreach (var character in characters)
            {
                var plws = Directory.GetFiles(character);
                foreach (var expectedFile in expectedFiles)
                {
                    var pp = Path.Combine(character, expectedFile);
                    if (!File.Exists(pp))
                    {
                        _output.WriteLine($"Missing: {character}\\{expectedFile}");
                        result = false;
                    }
                }
            }
            return result;
        }
    }
}
