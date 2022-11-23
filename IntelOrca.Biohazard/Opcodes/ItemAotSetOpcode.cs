using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("item_aot_set")]
    internal class ItemAotSetOpcode : OpcodeBase, IItemAotSetOpcode
    {
        public byte Id { get; set; }
        public byte SCE { get; set; }
        public byte SAT { get; set; }
        public byte Floor { get; set; }
        public byte Super { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public short W { get; set; }
        public short H { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }
        public ushort Array8Idx { get; set; }
        public byte MD1 { get; set; }
        public byte Action { get; set; }

        public byte[] Re1Unk { get; set; } = new byte[0];

        public static ItemAotSetOpcode Read(BinaryReader br, int offset)
        {
            var opcode = br.ReadByte();
            if ((OpcodeV1)opcode == OpcodeV1.ItemAotSet)
            {
                var op = new ItemAotSetOpcode();
                op.Offset = offset;
                op.Length = 18;

                op.Id = br.ReadByte();
                op.X = br.ReadInt16();
                op.Y = br.ReadInt16();
                op.W = br.ReadInt16();
                op.H = br.ReadInt16();
                op.Type = br.ReadByte();
                op.Re1Unk = br.ReadBytes(7);
                return op;
            }
            else
            {
                return new ItemAotSetOpcode()
                {
                    Offset = offset,
                    Length = 22,

                    Opcode = br.ReadByte(),
                    Id = br.ReadByte(),
                    SCE = br.ReadByte(),
                    SAT = br.ReadByte(),
                    Floor = br.ReadByte(),
                    Super = br.ReadByte(),
                    X = br.ReadInt16(),
                    Y = br.ReadInt16(),
                    W = br.ReadInt16(),
                    H = br.ReadInt16(),
                    Type = br.ReadUInt16(),
                    Amount = br.ReadUInt16(),
                    Array8Idx = br.ReadUInt16(),
                    MD1 = br.ReadByte(),
                    Action = br.ReadByte()
                };
            }
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Id);
            bw.Write(SCE);
            bw.Write(SAT);
            bw.Write(Floor);
            bw.Write(Super);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(W);
            bw.Write(H);
            bw.Write(Type);
            bw.Write(Amount);
            bw.Write(Array8Idx);
            bw.Write(MD1);
            bw.Write(Action);
        }
    }
}
