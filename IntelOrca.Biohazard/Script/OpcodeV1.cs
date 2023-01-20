namespace IntelOrca.Biohazard.Script
{
    internal enum OpcodeV1 : byte
    {
        End,
        IfelCk,
        ElseCk,
        EndIf,
        Ck,
        Set,
        Cmp6,
        Cmp7,
        Set8,
        CutSet9,
        CutSetA,
        DoorAotSe = 0x0C,
        NonItemSet = 0x0D,
        Nop,
        TestItem = 0x10,
        TestPickup = 0x11,
        Item12 = 0x12,
        ItemAotSet = 0x18,
        SceEmSet = 0x1B,
        OmSet = 0x1F
    }
}
