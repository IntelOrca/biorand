using System;
using System.Globalization;

namespace rer
{
    internal struct RdtId : IEquatable<RdtId>
    {
        public int Stage { get; }
        public int Room { get; }

        public RdtId(int stage, int room) : this()
        {
            Stage = stage;
            Room = room;
        }

        public override bool Equals(object? obj) => obj is RdtId id && Equals(id);
        public bool Equals(RdtId other) => Stage == other.Stage && Room == other.Room;
        public override int GetHashCode() => HashCode.Combine(Stage, Room);
        public static bool operator ==(RdtId left, RdtId right) => left.Equals(right);
        public static bool operator !=(RdtId left, RdtId right) => !(left == right);

        public static RdtId Parse(string s)
        {
            if (!TryParse(s, out var id))
                throw new FormatException("Failed to parse RDT ID.");
            return id;
        }

        public static bool TryParse(string s, out RdtId id)
        {
            id = default(RdtId);
            if (s.Length < 2)
                return false;
            var c = char.ToUpper(s[0]);

            int stage;
            if (c >= '0' && c <= '9')
            {
                stage = c - '0';
            }
            else if (c >= 'A' && c <= 'G')
            {
                stage = 10 + (c - 'A');
            }
            else
            {
                return false;
            }

            if (!int.TryParse(s[1..], NumberStyles.HexNumber, null, out var room))
                return false;

            id = new RdtId(stage - 1, room);
            return true;
        }

        public override string ToString()
        {
            if (Stage == 15)
                return $"G{Room:X2}";
            return $"{Stage + 1:X}{Room:X2}";
        }
    }
}
