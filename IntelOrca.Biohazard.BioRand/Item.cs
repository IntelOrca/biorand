namespace IntelOrca.Biohazard.BioRand
{
    internal readonly struct Item
    {
        public byte Type { get; }
        public byte Amount { get; }

        public Item(byte type, byte amount)
        {
            Type = type;
            Amount = amount;
        }

        public override string ToString() => $"Type = {Type} Amount = {Amount}";
    }
}
