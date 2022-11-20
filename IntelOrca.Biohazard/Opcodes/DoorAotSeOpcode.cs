using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{

    [DebuggerDisplay("{Opcode}")]
    internal class DoorAotSeOpcode : OpcodeBase, IDoorAotSetOpcode
    {
        public override Opcode Opcode => Opcode.DoorAotSe;
        public override int Length => 32;

        public byte Id { get; set; }
        public byte SCE { get; set; }
        public byte SAT { get; set; }
        public byte Floor { get; set; }
        public byte Super { get; set; }
        public short X { get; set; }
        public short Z { get; set; }
        public ushort W { get; set; }
        public ushort D { get; set; }
        public short NextX { get; set; }
        public short NextY { get; set; }
        public short NextZ { get; set; }
        public short NextD { get; set; }
        public byte NextStage { get; set; }
        public byte NextRoom { get; set; }
        public byte NextCamera { get; set; }
        public byte NextFloor { get; set; }
        public byte Texture { get; set; }
        public byte Animation { get; set; }
        public byte Sound { get; set; }
        public byte LockId { get; set; }
        public byte LockType { get; set; }
        public byte Free { get; set; }

        public RdtId Target
        {
            get => new RdtId(NextStage, NextRoom);
            set
            {
                NextStage = (byte)value.Stage;
                NextRoom = (byte)value.Room;
            }
        }

        public static DoorAotSeOpcode Read(BinaryReader br, int offset)
        {
            return new DoorAotSeOpcode()
            {
                Offset = offset,
                Id = br.ReadByte(),
                SCE = br.ReadByte(),
                SAT = br.ReadByte(),
                Floor = br.ReadByte(),
                Super = br.ReadByte(),
                X = br.ReadInt16(),
                Z = br.ReadInt16(),
                W = br.ReadUInt16(),
                D = br.ReadUInt16(),
                NextX = br.ReadInt16(),
                NextY = br.ReadInt16(),
                NextZ = br.ReadInt16(),
                NextD = br.ReadInt16(),
                NextStage = br.ReadByte(),
                NextRoom = br.ReadByte(),
                NextCamera = br.ReadByte(),
                NextFloor = br.ReadByte(),
                Texture = br.ReadByte(),
                Animation = br.ReadByte(),
                Sound = br.ReadByte(),
                LockId = br.ReadByte(),
                LockType = br.ReadByte(),
                Free = br.ReadByte()
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
            bw.Write(X);
            bw.Write(Z);
            bw.Write(W);
            bw.Write(D);
            bw.Write(NextX);
            bw.Write(NextY);
            bw.Write(NextZ);
            bw.Write(NextD);
            bw.Write(NextStage);
            bw.Write(NextRoom);
            bw.Write(NextCamera);
            bw.Write(NextFloor);
            bw.Write(Texture);
            bw.Write(Animation);
            bw.Write(Sound);
            bw.Write(LockId);
            bw.Write(LockType);
            bw.Write(Free);
        }
    }
}
