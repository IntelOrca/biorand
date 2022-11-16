using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("{Opcode} Id = {Id} Type = {Type} Amount = {Amount}")]
    internal class ItemAotSet4pOpcode : OpcodeBase, IItemAotSetOpcode
    {
        public override Opcode Opcode => Opcode.ItemAotSet4p;
        public override int Length => 30;

        public byte Id { get; set; }
        public byte SCE { get; set; }
        public byte SAT { get; set; }
        public byte Floor { get; set; }
        public byte Super { get; set; }
        public short X0 { get; set; }
        public short Z0 { get; set; }
        public short X1 { get; set; }
        public short Z1 { get; set; }
        public short X2 { get; set; }
        public short Z2 { get; set; }
        public short X3 { get; set; }
        public short Z3 { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }
        public ushort Array8Idx { get; set; }
        public byte MD1 { get; set; }
        public byte Action { get; set; }

        public static ItemAotSet4pOpcode Read(BinaryReader br, int offset)
        {
            return new ItemAotSet4pOpcode()
            {
                Offset = offset,
                Id = br.ReadByte(),
                SCE = br.ReadByte(),
                SAT = br.ReadByte(),
                Floor = br.ReadByte(),
                Super = br.ReadByte(),
                X0 = br.ReadInt16(),
                Z0 = br.ReadInt16(),
                X1 = br.ReadInt16(),
                Z1 = br.ReadInt16(),
                X2 = br.ReadInt16(),
                Z2 = br.ReadInt16(),
                X3 = br.ReadInt16(),
                Z3 = br.ReadInt16(),
                Type = br.ReadUInt16(),
                Amount = br.ReadUInt16(),
                Array8Idx = br.ReadUInt16(),
                MD1 = br.ReadByte(),
                Action = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Id);
            bw.Write(SCE);
            bw.Write(SAT);
            bw.Write(Floor);
            bw.Write(Super);
            bw.Write(X0);
            bw.Write(Z0);
            bw.Write(X1);
            bw.Write(Z1);
            bw.Write(X2);
            bw.Write(Z2);
            bw.Write(X3);
            bw.Write(Z3);
            bw.Write(Type);
            bw.Write(Amount);
            bw.Write(Array8Idx);
            bw.Write(MD1);
            bw.Write(Action);
        }
    }
}
