using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("else")]
    internal class ElseCkOpcode : OpcodeBase
    {
        public byte Id { get; set; }
        public byte Unk1 { get; set; }
        public ushort BlockLength { get; set; }

        public static ElseCkOpcode Read(BinaryReader br, int offset)
        {
            return new ElseCkOpcode()
            {
                Offset = offset,
                Length = 4,

                Opcode = br.ReadByte(),
                Unk1 = br.ReadByte(),
                BlockLength = br.ReadUInt16()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Unk1);
            bw.Write(BlockLength);
        }
    }
}
