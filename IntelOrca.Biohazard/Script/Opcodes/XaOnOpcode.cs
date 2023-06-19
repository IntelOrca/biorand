using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("{Opcode} Channel = {Channel} Id = {Id}")]
    public class XaOnOpcode : OpcodeBase
    {
        public byte Channel { get; set; }
        public ushort Id { get; set; }

        public static XaOnOpcode Read(BinaryReader br, int offset)
        {
            return new XaOnOpcode()
            {
                Offset = offset,
                Length = 4,

                Opcode = br.ReadByte(),
                Channel = br.ReadByte(),
                Id = br.ReadUInt16()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Channel);
            bw.Write(Id);
        }
    }
}
