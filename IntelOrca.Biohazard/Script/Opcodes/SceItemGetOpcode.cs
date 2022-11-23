using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("sce_item_get")]
    internal class SceItemGetOpcode : OpcodeBase
    {
        public byte Type { get; set; }
        public byte Amount { get; set; }

        public static SceItemGetOpcode Read(BinaryReader br, int offset)
        {
            return new SceItemGetOpcode()
            {
                Offset = offset,
                Length = 3,

                Opcode = br.ReadByte(),
                Type = br.ReadByte(),
                Amount = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Type);
            bw.Write(Amount);
        }
    }
}
