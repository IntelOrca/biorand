using System.Diagnostics;
using System.IO;
using rer.Opcodes;

namespace rer
{
    [DebuggerDisplay("{Opcode}")]
    internal class GosubOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.Gosub;
        public override int Length => 2;

        public byte Index { get; set; }

        public static GosubOpcode Read(BinaryReader br, int offset)
        {
            return new GosubOpcode()
            {
                Offset = offset,
                Index = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Index);
        }
    }
}
