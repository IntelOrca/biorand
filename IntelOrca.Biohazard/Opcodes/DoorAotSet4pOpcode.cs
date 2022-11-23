using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("door_aot_set_4p")]
    internal class DoorAotSet4pOpcode : OpcodeBase, IDoorAotSetOpcode
    {
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

        public static DoorAotSet4pOpcode Read(BinaryReader br, int offset)
        {
            return new DoorAotSet4pOpcode()
            {
                Offset = offset,
                Length = 40,

                Opcode = br.ReadByte(),
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
            bw.Write(Opcode);
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
