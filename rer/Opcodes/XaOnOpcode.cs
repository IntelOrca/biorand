using System.Diagnostics;
using System.IO;

namespace rer.Opcodes
{
    [DebuggerDisplay("{Opcode} Channel = {Channel} Id = {Id}")]
    internal class XaOnOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.XaOn;
        public override int Length => 4;

        public byte Channel { get; set; }
        public ushort Id { get; set; }

        public static XaOnOpcode Read(BinaryReader br, int offset)
        {
            return new XaOnOpcode()
            {
                Offset = offset,
                Channel = br.ReadByte(),
                Id = br.ReadUInt16()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Channel);
            bw.Write(Id);
        }
    }
}
