using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Opcodes
{
    [DebuggerDisplay("{Opcode} Offset = {Offset}  Length = {Length}")]
    internal class UnknownOpcode : OpcodeBase
    {
        private byte[] _data;

        public UnknownOpcode(int offset, byte opcode, byte[] operands)
        {
            Offset = offset;
            Length = 1 + operands.Length;

            Opcode = opcode;
            _data = operands;
        }

        public static UnknownOpcode Read(BinaryReader br, int offset, int length)
        {
            var opcode = br.ReadByte();
            var data = br.ReadBytes(length - 1);
            return new UnknownOpcode(offset, opcode, data);
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(_data);
        }

        public void NopOut()
        {
            for (int i = 0; i < _data.Length; i++)
            {
                _data[i] = 0;
            }
        }
    }
}
