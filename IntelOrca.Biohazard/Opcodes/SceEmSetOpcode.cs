using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Opcodes
{
    [DebuggerDisplay("{Opcode} Id = {Id} Type = {Type} State = {State}")]
    internal class SceEmSetOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.SceEmSet;
        public override int Length => 21;

        public byte Unk01 { get; set; }
        public byte Id { get; set; }
        public EnemyType Type { get; set; }
        public byte State { get; set; }
        public byte Ai { get; set; }
        public byte Floor { get; set; }
        public byte SoundBank { get; set; }
        public byte Texture { get; set; }
        public byte KillId { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
        public short D { get; set; }
        public ushort Animation { get; set; }
        public byte Unk15 { get; set; }

        public static SceEmSetOpcode Read(BinaryReader br, int offset)
        {
            return new SceEmSetOpcode()
            {
                Offset = offset,
                Unk01 = br.ReadByte(),
                Id = br.ReadByte(),
                Type = (EnemyType)br.ReadByte(),
                State = br.ReadByte(),
                Ai = br.ReadByte(),
                Floor = br.ReadByte(),
                SoundBank = br.ReadByte(),
                Texture = br.ReadByte(),
                KillId = br.ReadByte(),
                X = br.ReadInt16(),
                Y = br.ReadInt16(),
                Z = br.ReadInt16(),
                D = br.ReadInt16(),
                Animation = br.ReadUInt16(),
                Unk15 = br.ReadByte(),
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Unk01);
            bw.Write(Id);
            bw.Write((byte)Type);
            bw.Write(State);
            bw.Write(Ai);
            bw.Write(Floor);
            bw.Write(SoundBank);
            bw.Write(Texture);
            bw.Write(KillId);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(Z);
            bw.Write(D);
            bw.Write(Animation);
            bw.Write(Unk15);
        }
    }
}
