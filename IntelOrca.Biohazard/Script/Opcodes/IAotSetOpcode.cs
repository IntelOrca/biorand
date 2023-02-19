namespace IntelOrca.Biohazard.Script.Opcodes
{
    internal interface IAotSetOpcode
    {
        int Offset { get; }
        byte Opcode { get; }

        byte Id { get; set; }
        byte SCE { get; set; }
        byte SAT { get; set; }
        byte Floor { get; set; }
        byte Super { get; set; }
        ushort Data0 { get; set; }
        ushort Data1 { get; set; }
        ushort Data2 { get; set; }
    }
}
