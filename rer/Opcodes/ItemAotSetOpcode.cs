using System.Diagnostics;
using System.IO;
using System.Reflection.Emit;
using rer.Opcodes;

namespace rer
{
    [DebuggerDisplay("{Opcode} Id = {Id} Type = {Type} Amount = {Amount}")]
    internal class ItemAotSetOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.ItemAotSet;
        public override int Length => 22;

        public byte Id;
        public int Unknown0;
        public short X;
        public short Y;
        public short W;
        public short H;
        public ushort Type;
        public ushort Amount;
        public ushort Array8Idx;
        public ushort Unknown1;

        public static ItemAotSetOpcode Read(BinaryReader br, int offset)
        {
            return new ItemAotSetOpcode()
            {
                Offset = offset,
                Id = br.ReadByte(),
                Unknown0 = br.ReadInt32(),
                X = br.ReadInt16(),
                Y = br.ReadInt16(),
                W = br.ReadInt16(),
                H = br.ReadInt16(),
                Type = br.ReadUInt16(),
                Amount = br.ReadUInt16(),
                Array8Idx = br.ReadUInt16(),
                Unknown1 = br.ReadUInt16(),
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Id);
            bw.Write(Unknown0);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(W);
            bw.Write(H);
            bw.Write(Type);
            bw.Write(Amount);
            bw.Write(Array8Idx);
            bw.Write(Unknown1);
        }
    }
}
