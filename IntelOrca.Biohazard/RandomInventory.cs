namespace IntelOrca.Biohazard
{
    internal class RandomInventory
    {
        public Entry[] Entries { get; }

        public RandomInventory(Entry[] entries)
        {
            Entries = entries;
        }

        public struct Entry
        {
            public byte Type { get; }
            public byte Count { get; }

            public Entry(byte type, byte count)
            {
                Type = type;
                Count = count;
            }
        }
    }
}
