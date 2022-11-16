using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("{Opcode}")]
    internal class CmpOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.Cmp;
        public override int Length => 6;

        public byte Unknown1 { get; set; }
        public byte Flag { get; set; }
        public byte Operator { get; set; }
        public short Value { get; set; }

        public static CmpOpcode Read(BinaryReader br, int offset)
        {
            return new CmpOpcode()
            {
                Offset = offset,
                Unknown1 = br.ReadByte(),
                Flag = br.ReadByte(),
                Operator = br.ReadByte(),
                Value = br.ReadInt16()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Unknown1);
            bw.Write(Flag);
            bw.Write(Operator);
            bw.Write(Value);
        }
    }
}
