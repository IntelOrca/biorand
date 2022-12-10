using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("ck")]
    internal class CkOpcode : OpcodeBase
    {
        public byte BitArray { get; set; }
        public byte Index { get; set; }
        public byte Value { get; set; }

        public static CkOpcode Read(BinaryReader br, int offset)
        {
            return new CkOpcode()
            {
                Offset = offset,
                Length = 4,

                Opcode = br.ReadByte(),
                BitArray = br.ReadByte(),
                Index = br.ReadByte(),
                Value = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(BitArray);
            bw.Write(Index);
            bw.Write(Value);
        }
    }
}
