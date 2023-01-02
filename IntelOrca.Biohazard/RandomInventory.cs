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
            public byte Part { get; }

            public Entry(byte type, byte count, byte part)
            {
                Type = type;
                Count = count;
                Part = part;
            }
        }
    }
}
