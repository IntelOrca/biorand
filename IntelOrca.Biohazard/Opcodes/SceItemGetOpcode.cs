using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Opcodes
{
    [DebuggerDisplay("{Opcode} Item = {Item} Amount = {Amount}")]
    internal class SceItemGetOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.SceItemGet;
        public override int Length => 3;

        public byte Type { get; set; }
        public byte Amount { get; set; }

        public static SceItemGetOpcode Read(BinaryReader br, int offset)
        {
            return new SceItemGetOpcode()
            {
                Offset = offset,
                Type = br.ReadByte(),
                Amount = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Type);
            bw.Write(Amount);
        }
    }
}
