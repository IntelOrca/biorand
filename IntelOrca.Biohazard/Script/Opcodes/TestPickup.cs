using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("testpickup")]
    public class TestPickupOpcode : OpcodeBase
    {
        public byte Type { get; set; }

        public static TestPickupOpcode Read(BinaryReader br, int offset)
        {
            return new TestPickupOpcode()
            {
                Offset = offset,
                Length = 2,

                Opcode = br.ReadByte(),
                Type = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Type);
        }
    }
}
