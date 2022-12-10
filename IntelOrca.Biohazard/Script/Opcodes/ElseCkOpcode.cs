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
            var opcode = br.ReadByte();
            if ((OpcodeV1)opcode == OpcodeV1.ElseCk)
            {
                var op = new ElseCkOpcode();
                op.Offset = offset;
                op.Length = 2;

                op.Opcode = opcode;
                op.BlockLength = br.ReadByte();
                return op;
            }
            else
            {
                return new ElseCkOpcode()
                {
                    Offset = offset,
                    Length = 4,

                    Opcode = opcode,
                    Unk1 = br.ReadByte(),
                    BlockLength = br.ReadUInt16()
                };
            }
        }

        public override void Write(BinaryWriter bw)
        {
            if ((OpcodeV1)Opcode == OpcodeV1.ElseCk)
            {
                bw.Write(Opcode);
                bw.Write((byte)BlockLength);
            }
            else
            {
                bw.Write(Opcode);
                bw.Write(Unk1);
                bw.Write(BlockLength);
            }
        }
    }
}
