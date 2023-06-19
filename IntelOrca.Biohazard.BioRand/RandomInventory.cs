namespace IntelOrca.Biohazard
{
    internal class RandomInventory
    {
        public Entry[] Entries { get; }
        public Entry? Special { get; }

        public RandomInventory(Entry[] entries, Entry? special)
        {
            Entries = entries;
            Special = special;
        }

        public struct Entry
        {
            public byte Type { get; }
            public byte Count { get; }
            public byte Part { get; }

            public Entry(byte type, byte count)
            {
                Type = type;
                Count = count;
                Part = 0;
            }

            public Entry(byte type, byte count, byte part)
            {
                Type = type;
                Count = count;
                Part = part;
            }
        }
    }
}
