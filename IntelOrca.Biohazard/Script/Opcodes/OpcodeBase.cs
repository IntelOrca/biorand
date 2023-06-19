using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    public abstract class OpcodeBase
    {
        public byte Opcode { get; set; }
        public int Length { get; set; }
        public int Offset { get; set; }

        public abstract void Write(BinaryWriter bw);
    }
}
