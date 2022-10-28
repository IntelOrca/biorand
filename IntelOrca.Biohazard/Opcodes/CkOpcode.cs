using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("{Opcode}")]
    internal class CkOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.Ck;
        public override int Length => 4;

        public byte BitArray { get; set; }
        public byte Index { get; set; }
        public byte Value { get; set; }

        public static CkOpcode Read(BinaryReader br, int offset)
        {
            return new CkOpcode()
            {
                Offset = offset,
                BitArray = br.ReadByte(),
                Index = br.ReadByte(),
                Value = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(BitArray);
            bw.Write(Index);
            bw.Write(Value);
        }
    }
}
