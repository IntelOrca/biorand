using System.IO;

namespace IntelOrca.Biohazard.Opcodes
{
    internal abstract class OpcodeBase
    {
        public int Offset { get; set; }
        public abstract int Length { get; }
        public abstract Opcode Opcode { get; }

        public abstract void Write(BinaryWriter bw);
    }
}
