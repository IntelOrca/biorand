using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("aot_reset")]
    internal class AotResetOpcode : OpcodeBase
    {
        public byte Id { get; set; }
        public byte SCE { get; set; }
        public byte SAT { get; set; }
        public ushort Data0 { get; set; }
        public ushort Data1 { get; set; }
        public ushort Data2 { get; set; }

        public short NextX
        {
            get => (short)Data0;
            set => Data0 = (ushort)value;
        }

        public short NextY
        {
            get => (short)Data1;
            set => Data1 = (ushort)value;
        }

        public short NextZ
        {
            get => (short)Data2;
            set => Data2 = (ushort)value;
        }

        public static AotResetOpcode Read(BinaryReader br, int offset)
        {
            return new AotResetOpcode()
            {
                Offset = offset,
                Length = 10,

                Opcode = br.ReadByte(),
                Id = br.ReadByte(),
                SCE = br.ReadByte(),
                SAT = br.ReadByte(),
                Data0 = br.ReadUInt16(),
                Data1 = br.ReadUInt16(),
                Data2 = br.ReadUInt16()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Id);
            bw.Write(SCE);
            bw.Write(SAT);
            bw.Write(Data0);
            bw.Write(Data1);
            bw.Write(Data2);
        }
    }
}
