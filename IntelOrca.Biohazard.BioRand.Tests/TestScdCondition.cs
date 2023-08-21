using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Script.Opcodes;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Tests
{
    public class TestScdCondition
    {
        [Theory]
        [InlineData("1:2")]
        [InlineData("1:5")]
        [InlineData("!2:4")]
        [InlineData("$3 == 10")]
        [InlineData("1:5 && 1:3")]
        [InlineData("1:5 && !1:3")]
        [InlineData("!1:5 && 1:3")]
        [InlineData("!1:5 && !1:3")]
        [InlineData("$3 == 10 && 1:3")]
        [InlineData("$3 == 10 && !1:3")]
        [InlineData("$3 != 10 && 1:3")]
        [InlineData("$3 != 10 && !1:3")]
        [InlineData("!($3 == 10 && 1:3)")]
        public void Parse(string expression)
        {
            var result = ScdCondition.Parse(expression);
            Assert.Equal(expression, result.ToString());
        }

        [Theory]
        [InlineData("!1:2", "01-0A-04-01-02-00-00-CD-00-CC-03-00")]
        [InlineData("!1:0 && !1:2", "01-0E-04-01-00-00-04-01-02-00-00-CD-00-CC-03-00")]
        public void Generate_1(string expression, string bytes)
        {
            var expr = ScdCondition.Parse(expression);
            var output = expr.Generate(BioVersion.Biohazard1, new[] {
                new UnknownOpcode(0, 0x00, new byte[] { 0xCD }),
                new UnknownOpcode(0, 0x00, new byte[] { 0xCC })
            });
            var outputBytes = Stringify(ToBytes(output));
            Assert.Equal(bytes, outputBytes);
        }

        [Theory]
        [InlineData("1:2", "06-00-0C-00-4C-01-02-01-00-CD-00-CC-08-00")]
        [InlineData("!(7:34 && 7:36 && 7:47 && 7:35)", "06-00-14-00-4C-07-22-01-4C-07-24-01-4C-07-2F-01-4C-07-23-01-07-00-10-00-00-CD-00-CC")]
        public void Generate_3(string expression, string bytes)
        {
            var expr = ScdCondition.Parse(expression);
            var output = expr.Generate(BioVersion.Biohazard3, new[] {
                new UnknownOpcode(0, 0x00, new byte[] { 0xCD }),
                new UnknownOpcode(0, 0x00, new byte[] { 0xCC })
            });
            var outputBytes = Stringify(ToBytes(output));
            Assert.Equal(bytes, outputBytes);
        }

        private static byte[] ToBytes(OpcodeBase[] opcodes)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            foreach (var opcode in opcodes)
            {
                opcode.Write(bw);
            }
            return ms.ToArray();
        }

        private static string Stringify(byte[] data)
        {
            return string.Join("-", data.Select(x => x.ToString("X2")));
        }
    }
}
