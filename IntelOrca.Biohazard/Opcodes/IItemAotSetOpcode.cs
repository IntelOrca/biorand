namespace IntelOrca.Biohazard
{
    internal interface IItemAotSetOpcode
    {
        int Offset { get; }
        Opcode Opcode { get; }

        byte Id { get; set; }
        byte SCE { get; set; }
        byte SAT { get; set; }
        byte Floor { get; set; }
        byte Super { get; set; }
        ushort Type { get; set; }
        ushort Amount { get; set; }
        ushort Array8Idx { get; set; }
        byte MD1 { get; set; }
        byte Action { get; set; }
    }
}
