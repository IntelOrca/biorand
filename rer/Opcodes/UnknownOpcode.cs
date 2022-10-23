using System.Diagnostics;
using System.IO;

namespace rer.Opcodes
{
    [DebuggerDisplay("{Opcode} Offset = {Offset}  Length = {Length}")]
    internal class UnknownOpcode : OpcodeBase
    {
        private byte[] _data;

        public override Opcode Opcode => (Opcode)_data[0];
        public override int Length => _data.Length;

        public UnknownOpcode(int offset, byte[] data)
        {
            Offset = offset;
            _data = data;
        }

        public static UnknownOpcode Read(BinaryReader br, Opcode opcode, int offset, int length)
        {
            var data = new byte[length];
            data[0] = (byte)opcode;
            br.Read(data, 1, length - 1);
            return new UnknownOpcode(offset, data);
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
