using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("aot_set_4p")]
    internal class AotSet4pOpcode : OpcodeBase, IAotSetOpcode
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
        public ushort Data0 { get; set; }
        public ushort Data1 { get; set; }
        public ushort Data2 { get; set; }

        public static AotSet4pOpcode Read(BinaryReader br, int offset)
        {
            return new AotSet4pOpcode()
            {
                Offset = offset,
                Length = 28,

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
                Data0 = br.ReadUInt16(),
                Data1 = br.ReadUInt16(),
                Data2 = br.ReadUInt16(),
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
            bw.Write(Data0);
            bw.Write(Data1);
            bw.Write(Data2);
        }
    }
}
