using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("{Opcode}")]
    internal class ElseCkOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.ElseCk;
        public override int Length => 4;

        public byte Id { get; set; }
        public byte Unk1 { get; set; }
        public ushort BlockLength { get; set; }

        public static ElseCkOpcode Read(BinaryReader br, int offset)
        {
            return new ElseCkOpcode()
            {
                Offset = offset,
                Unk1 = br.ReadByte(),
                BlockLength = br.ReadUInt16()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Unk1);
            bw.Write(BlockLength);
        }
    }
}
