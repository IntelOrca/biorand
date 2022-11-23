using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.Opcodes
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

        public byte Re1Unk0 { get; set; }
        public byte Re1Unk1 { get; set; }
        public byte Re1Unk2 { get; set; }
        public byte Re1Unk3 { get; set; }
        public byte Re1Unk4 { get; set; }
        public byte Re1Unk5 { get; set; }
        public byte Re1Unk6 { get; set; }
        public byte Re1Unk7 { get; set; }
        public byte Re1Unk8 { get; set; }
        public byte Re1Unk9 { get; set; }

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
                op.Re1Unk0 = br.ReadByte();
                op.Re1Unk1 = br.ReadByte();
                op.Re1Unk2 = br.ReadByte();
                op.Re1Unk3 = br.ReadByte();
                op.Re1Unk4 = br.ReadByte();
                op.D = br.ReadInt16();
                op.Re1Unk5 = br.ReadByte();
                op.Re1Unk6 = br.ReadByte();
                op.X = br.ReadInt16();
                op.Y = br.ReadInt16();
                op.Z = br.ReadInt16();
                op.Id = br.ReadByte();
                op.Re1Unk7 = br.ReadByte();
                op.Re1Unk8 = br.ReadByte();
                op.Re1Unk9 = br.ReadByte();
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
