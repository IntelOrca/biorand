using System;

namespace IntelOrca.Biohazard
{
    public struct EnemyPosition : IEquatable<EnemyPosition>
    {
        private RdtId _rdtId;

        public RdtId RdtId => _rdtId;

        public string? Room
        {
            get => _rdtId.ToString();
            set
            {
                _rdtId = value == null ? default(RdtId) : RdtId.Parse(value);
            }
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public int F { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is EnemyPosition pos ? Equals(pos) : false;
        }

        public bool Equals(EnemyPosition other)
        {
            return other is EnemyPosition position &&
                   Room == position.Room &&
                   X == position.X &&
                   Y == position.Y &&
                   Z == position.Z &&
                   D == position.D &&
                   F == position.F;
        }

        public override int GetHashCode()
        {
            return (Room?.GetHashCode() ?? 0) ^ X ^ Y ^ Z ^ D ^ F;
        }
    }
}
