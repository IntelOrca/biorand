namespace rer
{
    internal struct ItemPoolEntry
    {
        public int Stage { get; set; }
        public int Room { get; set; }
        public byte Id { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }

        public override string ToString()
        {
            return $"{Utility.GetHumanRoomId(Stage, Room)},{Id}";
        }
    }
}
