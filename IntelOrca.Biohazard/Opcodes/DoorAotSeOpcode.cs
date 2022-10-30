using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("{Opcode}")]
    internal class DoorAotSeOpcode : OpcodeBase
    {
        public override Opcode Opcode => Opcode.DoorAotSe;
        public override int Length => 32;

        public byte Id;
        public ushort Unknown2;
        public ushort Unknown4;
        public short X;
        public short Y;
        public short W;
        public short H;
        public short NextX;
        public short NextY;
        public short NextZ;
        public short NextD;
        public byte Stage;
        public byte Room;
        public byte Camera;
        public byte Unknown19;
        public byte DoorType;
        public byte DoorFlag;
        public byte Unknown1C;
        public byte DoorLockFlag;
        public byte DoorKey;
        public byte Unknown1F;

        public RdtId Target
        {
            get => new RdtId(Stage, Room);
            set
            {
                Stage = (byte)value.Stage;
                Room = (byte)value.Room;
            }
        }

        public static DoorAotSeOpcode Read(BinaryReader br, int offset)
        {
            return new DoorAotSeOpcode()
            {
                Offset = offset,
                Id = br.ReadByte(),
                Unknown2 = br.ReadUInt16(),
                Unknown4 = br.ReadUInt16(),
                X = br.ReadInt16(),
                Y = br.ReadInt16(),
                W = br.ReadInt16(),
                H = br.ReadInt16(),
                NextX = br.ReadInt16(),
                NextY = br.ReadInt16(),
                NextZ = br.ReadInt16(),
                NextD = br.ReadInt16(),
                Stage = br.ReadByte(),
                Room = br.ReadByte(),
                Camera = br.ReadByte(),
                Unknown19 = br.ReadByte(),
                DoorType = br.ReadByte(),
                DoorFlag = br.ReadByte(),
                Unknown1C = br.ReadByte(),
                DoorLockFlag = br.ReadByte(),
                DoorKey = br.ReadByte(),
                Unknown1F = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write((byte)Opcode);
            bw.Write(Id);
            bw.Write(Unknown2);
            bw.Write(Unknown4);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(W);
            bw.Write(H);
            bw.Write(NextX);
            bw.Write(NextY);
            bw.Write(NextZ);
            bw.Write(NextD);
            bw.Write(Stage);
            bw.Write(Room);
            bw.Write(Camera);
            bw.Write(Unknown19);
            bw.Write(DoorType);
            bw.Write(DoorFlag);
            bw.Write(Unknown1C);
            bw.Write(DoorLockFlag);
            bw.Write(DoorKey);
            bw.Write(Unknown1F);
        }
    }
}
