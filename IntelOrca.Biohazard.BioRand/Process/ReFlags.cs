using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.BioRand.Process
{
    public readonly struct ReFlags : IEquatable<ReFlags>
    {
        private readonly byte[] _flags;

        public int Count => _flags == null ? 0 : _flags.Length * 8;
        public int ByteLength => _flags == null ? 0 : _flags.Length;

        public ReFlags(ReadOnlySpan<byte> src)
        {
            _flags = src.ToArray();
        }

        public int[] Keys
        {
            get
            {
                var keys = new List<int>();
                var numFlags = Count;
                for (var i = 0; i < numFlags; i++)
                {
                    if (this[i])
                        keys.Add(i);
                }
                return keys.ToArray();
            }
        }

        // public bool this[int index] => (_flags[index >> 3] & (1 << (index & 7))) != 0;
        public bool this[int index]
        {
            get
            {
                var ints = MemoryMarshal.Cast<byte, int>(_flags);
                var idx = ints[index >> 5];
                var mask = 1 << (32 - index - 1 & 31);
                return (idx & mask) != 0;
            }
        }
        public static bool operator ==(ReFlags lhs, ReFlags rhs) => lhs.Equals(rhs);
        public static bool operator !=(ReFlags lhs, ReFlags rhs) => !(lhs == rhs);

        public static ReFlags operator ^(ReFlags lhs, ReFlags rhs)
        {
            var minLength = Math.Min(lhs.ByteLength, rhs.ByteLength);
            var resultFlags = new byte[minLength];
            for (var i = 0; i < minLength; i++)
            {
                resultFlags[i] = (byte)(lhs._flags[i] ^ rhs._flags[i]);
            }
            return new ReFlags(resultFlags);
        }

        public static ReFlags operator &(ReFlags lhs, ReFlags rhs)
        {
            var minLength = Math.Min(lhs.ByteLength, rhs.ByteLength);
            var resultFlags = new byte[minLength];
            for (var i = 0; i < minLength; i++)
            {
                resultFlags[i] = (byte)(lhs._flags[i] & rhs._flags[i]);
            }
            return new ReFlags(resultFlags);
        }

        public override bool Equals(object obj) => obj is ReFlags flags && Equals(flags);

        public bool Equals(ReFlags other)
        {
            if (_flags is null && other._flags is null)
                return true;
            if (_flags is null || other._flags is null)
                return false;
            return _flags.SequenceEqual(other._flags);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                foreach (byte flag in _flags)
                {
                    hash = hash * 31 + flag.GetHashCode();
                }
            }
            return hash;
        }
    }
}
