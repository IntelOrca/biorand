using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("sce_em_set")]
    internal class SceEmSetOpcode : OpcodeBase
    {
        public byte Unk01 { get; set; }
        public byte Id { get; set; }
        public byte Type { get; set; }
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

        public byte Re1Unk04 { get; set; }
        public byte Re1Unk05 { get; set; }
        public byte Re1Unk06 { get; set; }
        public byte Re1Unk07 { get; set; }
        public byte Re1Unk0A { get; set; }
        public byte Re1Unk0B { get; set; }
        public byte Re1Unk13 { get; set; }
        public byte Re1Unk14 { get; set; }
        public byte Re1Unk15 { get; set; }

        public static SceEmSetOpcode Read(BinaryReader br, int offset)
        {
            var opcode = br.ReadByte();
            if ((OpcodeV1)opcode == OpcodeV1.SceEmSet)
            {
                var op = new SceEmSetOpcode();
                op.Offset = offset;
                op.Length = 22;

                op.Opcode = opcode;
                op.Type = br.ReadByte();
                op.State = br.ReadByte();
                op.KillId = br.ReadByte();
                op.Re1Unk04 = br.ReadByte();
                op.Re1Unk05 = br.ReadByte();
                op.Re1Unk06 = br.ReadByte();
                op.Re1Unk07 = br.ReadByte();
                op.D = br.ReadInt16();
                op.Re1Unk0A = br.ReadByte();
                op.Re1Unk0B = br.ReadByte();
                op.X = br.ReadInt16();
                op.Y = br.ReadInt16();
                op.Z = br.ReadInt16();
                op.Id = br.ReadByte();
                op.Re1Unk13 = br.ReadByte();
                op.Re1Unk14 = br.ReadByte();
                op.Re1Unk15 = br.ReadByte();
                return op;
            }
            else
            {
                return new SceEmSetOpcode()
                {
                    Offset = offset,
                    Length = 21,

                    Opcode = opcode,
                    Unk01 = br.ReadByte(),
                    Id = br.ReadByte(),
                    Type = br.ReadByte(),
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
        }

        public override void Write(BinaryWriter bw)
        {
            if ((OpcodeV1)Opcode == OpcodeV1.SceEmSet)
            {
                bw.Write(Opcode);
                bw.Write(Type);
                bw.Write(State);
                bw.Write(KillId);
                bw.Write(Re1Unk04);
                bw.Write(Re1Unk05);
                bw.Write(Re1Unk06);
                bw.Write(Re1Unk07);
                bw.Write(D);
                bw.Write(Re1Unk0A);
                bw.Write(Re1Unk0B);
                bw.Write(X);
                bw.Write(Y);
                bw.Write(Z);
                bw.Write(Id);
                bw.Write(Re1Unk13);
                bw.Write(Re1Unk14);
                bw.Write(Re1Unk15);
            }
            else
            {
                bw.Write(Opcode);
                bw.Write(Unk01);
                bw.Write(Id);
                bw.Write(Type);
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
}
