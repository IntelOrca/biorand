using System;

namespace IntelOrca.Biohazard.BioRand
{
    internal readonly struct Item : IEquatable<Item>
    {
        public byte Type { get; }
        public byte Amount { get; }

        public Item(byte type, byte amount)
        {
            Type = type;
            Amount = amount;
        }

        public override string ToString() => $"Type = {Type} Amount = {Amount}";

        public override bool Equals(object? obj)
        {
            return obj is Item item && Equals(item);
        }

        public bool Equals(Item other)
        {
            return Type == other.Type &&
                   Amount == other.Amount;
        }

        public override int GetHashCode()
        {
            int hashCode = -1636817442;
            hashCode = hashCode * -1521134295 + Type.GetHashCode();
            hashCode = hashCode * -1521134295 + Amount.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Item left, Item right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Item left, Item right)
        {
            return !(left == right);
        }
    }
}
