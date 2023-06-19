using System;
using System.Text;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard
{
    public struct RdtDoorTarget : IEquatable<RdtDoorTarget>
    {
        private static Regex g_regex = new Regex(@"([0-9A-Za-z]+)(?:\:(\d+))?(?:%(\d+))?", RegexOptions.Compiled);

        public RdtId Rdt { get; }
        public byte? Id { get; }
        public byte? Cut { get; }

        public RdtDoorTarget(RdtId rtd, byte? id, byte? cut) : this()
        {
            Rdt = rtd;
            Id = id;
            Cut = cut;
        }

        public override bool Equals(object? obj) => obj is RdtDoorTarget id && Equals(id);
        public bool Equals(RdtDoorTarget other) => Rdt == other.Rdt && Id == other.Id && Cut == other.Cut;

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Rdt.GetHashCode();
            hash = hash * 23 + Id.GetHashCode();
            hash = hash * 23 + Cut.GetHashCode();
            return hash;
        }

        public static bool operator ==(RdtDoorTarget left, RdtDoorTarget right) => left.Equals(right);
        public static bool operator !=(RdtDoorTarget left, RdtDoorTarget right) => !(left == right);

        public static RdtDoorTarget Parse(string s)
        {
            if (!TryParse(s, out var id))
                throw new FormatException("Failed to parse RDT DOOR TARGET.");
            return id;
        }

        public static bool TryParse(string s, out RdtDoorTarget value)
        {
            value = default(RdtDoorTarget);

            var match = g_regex.Match(s);
            if (!match.Success)
                return false;
            var doorId = (byte?)null;
            var cut = (byte?)null;


            if (!RdtId.TryParse(match.Groups[1].Value, out var rdtId))
                return false;

            if (match.Groups[2].Success)
            {
                if (!byte.TryParse(match.Groups[2].Value, out var temp))
                    return false;
                doorId = temp;
            }
            if (match.Groups[3].Success)
            {
                if (!byte.TryParse(match.Groups[3].Value, out var temp))
                    return false;
                cut = temp;
            }

            value = new RdtDoorTarget(rdtId, doorId, cut);
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Rdt);
            if (Id != null)
            {
                sb.Append(':');
                sb.Append(Id);
            }
            if (Cut != null)
            {
                sb.Append('%');
                sb.Append(Cut);
            }
            return sb.ToString();
        }
    }
}
