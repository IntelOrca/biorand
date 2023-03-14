using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("evt_exec")]
    internal class EvtExecOpcode : OpcodeBase
    {
        public byte Unknown { get; set; }
        public byte BackgroundOpcode { get; set; }
        public byte BackgroundOperand { get; set; }

        public static EvtExecOpcode Read(BinaryReader br, int offset)
        {
            return new EvtExecOpcode()
            {
                Offset = offset,
                Length = 2,

                Opcode = br.ReadByte(),
                Unknown = br.ReadByte(),
                BackgroundOpcode = br.ReadByte(),
                BackgroundOperand = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Unknown);
            bw.Write(BackgroundOpcode);
            bw.Write(BackgroundOperand);
        }
    }
}
