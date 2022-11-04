namespace IntelOrca.Biohazard
{
    internal struct DoorEntrance
    {
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
        public short D { get; set; }
        public byte Camera { get; set; }
        public byte Floor { get; set; }

        public static DoorEntrance FromOpcode(IDoorAotSetOpcode opcode)
        {
            return new DoorEntrance()
            {
                X = opcode.NextX,
                Y = opcode.NextY,
                Z = opcode.NextZ,
                D = opcode.NextD,
                Camera = opcode.NextCamera,
                Floor = opcode.NextFloor
            };
        }
    }
}
