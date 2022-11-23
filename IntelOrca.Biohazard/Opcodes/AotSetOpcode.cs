using System.Diagnostics;
using System.IO;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    [DebuggerDisplay("aot_set")]
    internal class AotSetOpcode : OpcodeBase
    {
        public byte Id { get; set; }
        public byte SCE { get; set; }
        public byte SAT { get; set; }
        public byte Floor { get; set; }
        public byte Super { get; set; }
        public short X { get; set; }
        public short Z { get; set; }
        public ushort W { get; set; }
        public ushort D { get; set; }
        public ushort Data0 { get; set; }
        public ushort Data1 { get; set; }
        public ushort Data2 { get; set; }

        public static AotSetOpcode Read(BinaryReader br, int offset)
        {
            return new AotSetOpcode()
            {
                Offset = offset,
                Length = 10,

                Opcode = br.ReadByte(),
                Id = br.ReadByte(),
                SCE = br.ReadByte(),
                SAT = br.ReadByte(),
                Floor = br.ReadByte(),
                Super = br.ReadByte(),
                X = br.ReadInt16(),
                Z = br.ReadInt16(),
                W = br.ReadUInt16(),
                D = br.ReadUInt16(),
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
            bw.Write(X);
            bw.Write(Z);
            bw.Write(W);
            bw.Write(D);
            bw.Write(Data0);
            bw.Write(Data1);
            bw.Write(Data2);
        }
    }
}
