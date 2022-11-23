using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
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

        public ulong Re1Unk0C { get; set; }
        public uint Re1Unk14 { get; set; }
        public byte TakeAnimation { get; set; }
        public uint Re1Unk19 { get; set; }

        public static ItemAotSetOpcode Read(BinaryReader br, int offset)
        {
            var opcode = br.ReadByte();
            if ((OpcodeV1)opcode == OpcodeV1.ItemAotSet)
            {
                // 18 NN XX XX ZZ ZZ ?? ?? ?? ?? HH VV ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? TA ??
                // 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17 18 19
                var op = new ItemAotSetOpcode();
                op.Offset = offset;
                op.Length = 26;

                op.Opcode = opcode;
                op.Id = br.ReadByte();
                op.X = br.ReadInt16();
                op.Y = br.ReadInt16();
                op.W = br.ReadInt16();
                op.H = br.ReadInt16();
                op.Type = br.ReadByte();
                op.Amount = br.ReadByte();
                op.Re1Unk0C = br.ReadUInt64();
                op.Re1Unk14 = br.ReadUInt32();
                op.TakeAnimation = br.ReadByte();
                op.Re1Unk19 = br.ReadByte();
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
