using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("{Opcode}")]
    internal class AotResetOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.AotReset;
        public override int Length => 10;

        public byte Id { get; set; }
        public ushort Unk2 { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }
        public ushort Unk8 { get; set; }

        public static AotResetOpcode Read(BinaryReader br, int offset)
        {
            return new AotResetOpcode()
            {
                Offset = offset,
                Id = br.ReadByte(),
                Unk2 = br.ReadUInt16(),
                Type = br.ReadUInt16(),
                Amount = br.ReadUInt16(),
                Unk8 = br.ReadUInt16(),
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Id);
            bw.Write(Unk2);
            bw.Write(Type);
            bw.Write(Amount);
            bw.Write(Unk8);
        }
    }
}
