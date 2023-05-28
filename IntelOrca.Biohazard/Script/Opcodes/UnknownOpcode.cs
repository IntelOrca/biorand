using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("{Opcode} Offset = {Offset}  Length = {Length}")]
    internal class UnknownOpcode : OpcodeBase
    {
        public byte[] Data { get; }

        public UnknownOpcode(int offset, byte opcode, byte[] operands)
        {
            Offset = offset;
            Length = 1 + operands.Length;

            Opcode = opcode;
            Data = operands;
        }

        public static UnknownOpcode Read(BinaryReader br, int offset, int length)
        {
            var opcode = br.ReadByte();
            var data = br.ReadBytes(length - 1);
            return new UnknownOpcode(offset, opcode, data);
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Data);
        }

        public void NopOut(BioVersion version)
        {
            var code = version == BioVersion.Biohazard1 ? (byte)OpcodeV1.Nop : (byte)OpcodeV2.Nop;
            Opcode = code;
            for (int i = 0; i < Data.Length; i++)
            {
                Data[i] = code;
            }
        }
    }
}
