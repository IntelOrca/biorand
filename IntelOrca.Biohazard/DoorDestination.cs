namespace IntelOrca.Biohazard
{
    internal struct DoorDestination
    {
        public RdtId RdtId { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
        public short D { get; set; }
        public byte Camera { get; set; }

        public static DoorDestination FromOpcode(DoorAotSeOpcode opcode)
        {
            return new DoorDestination()
            {
                RdtId = opcode.Target,
                X = opcode.NextX,
                Y = opcode.NextY,
                Z = opcode.NextZ,
                D = opcode.NextD,
                Camera = opcode.Camera
            };
        }
    }

    internal struct PlayNodeDoor
    {
        public int? Id { get; set; }
        public DoorDestination DestinationForThisRoom { get; set; }
    }
}
