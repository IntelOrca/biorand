using System;

namespace rer
{
    internal struct RdtItemId : IEquatable<RdtItemId>
    {
        public RdtId Rdt { get; }
        public byte Id { get; }

        public RdtItemId(RdtId rtd, byte id) : this()
        {
            Rdt = rtd;
            Id = id;
        }

        public override bool Equals(object? obj) => obj is RdtItemId id && Equals(id);
        public bool Equals(RdtItemId other) => Rdt == other.Rdt && Id == other.Id;
        public override int GetHashCode() => HashCode.Combine(Rdt, Id);
        public static bool operator ==(RdtItemId left, RdtItemId right) => left.Equals(right);
        public static bool operator !=(RdtItemId left, RdtItemId right) => !(left == right);

        public static RdtItemId Parse(string s)
        {
            if (!TryParse(s, out var id))
                throw new FormatException("Failed to parse RDT ITEM ID.");
            return id;
        }

        public static bool TryParse(string s, out RdtItemId value)
        {
            value = default(RdtItemId);

            var colonIndex = s.IndexOf(":");
            if (colonIndex == -1)
                return false;

            var left = s.Substring(0, colonIndex);
            if (!RdtId.TryParse(left, out var rtd))
                return false;

            var right = s.Substring(colonIndex + 1);
            if (!byte.TryParse(right, out var id))
                return false;

            value = new RdtItemId(rtd, id);
            return true;
        }

        public override string ToString() => $"{Rdt}:{Id}";
    }
}
