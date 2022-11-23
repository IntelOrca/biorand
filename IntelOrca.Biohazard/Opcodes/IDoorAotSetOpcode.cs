namespace IntelOrca.Biohazard
{
    internal interface IDoorAotSetOpcode
    {
        int Offset { get; }
        byte Opcode { get; }
        RdtId Target { get; set; }

        byte Id { get; set; }
        byte SCE { get; set; }
        byte SAT { get; set; }
        byte Floor { get; set; }
        byte Super { get; set; }
        short NextX { get; set; }
        short NextY { get; set; }
        short NextZ { get; set; }
        short NextD { get; set; }
        byte NextStage { get; set; }
        byte NextRoom { get; set; }
        byte NextCamera { get; set; }
        byte NextFloor { get; set; }
        byte Texture { get; set; }
        byte Animation { get; set; }
        byte Sound { get; set; }
        byte LockId { get; set; }
        byte LockType { get; set; }
        byte Free { get; set; }
    }
}
