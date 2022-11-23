namespace IntelOrca.Biohazard.Script
{
    internal enum OpcodeV1 : byte
    {
        Nop,
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
        ItemAotSet = 0x0D,
        Item12 = 0x12,
        ObjModelSet = 0x18,
        SceEmSet = 0x1B,
        OmSet = 0x1F
    }
}
