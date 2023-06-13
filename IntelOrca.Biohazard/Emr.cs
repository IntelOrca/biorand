using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    public class Emr
    {
        private byte[] _data;

        public Emr(byte[] data)
        {
            _data = data;
        }

        public byte[] GetBytes() => _data;

        public int Unk0 => BitConverter.ToInt16(_data, 0);
        public int DataOffset => BitConverter.ToInt16(_data, 2);
        public int NumParts => BitConverter.ToInt16(_data, 4);
        public int DataWidth => BitConverter.ToInt16(_data, 6);

        public Vector GetRelativePosition(int partIndex)
        {
            if (partIndex < 0 || partIndex >= NumParts)
                throw new ArgumentOutOfRangeException(nameof(partIndex));

            var offset = 8 + (partIndex * 6);
            var values = GetSpan<short>(offset, 3);
            return new Vector()
            {
                x = values[0],
                y = values[1],
                z = values[2]
            };
        }

        public Armature GetArmature(int partIndex)
        {
            if (partIndex < 0 || partIndex >= NumParts)
                throw new ArgumentOutOfRangeException(nameof(partIndex));

            var offset = Unk0 + (partIndex * 4);
            var values = GetSpan<short>(offset, 2);
            return new Armature()
            {
                count = values[0],
                offset = values[1]
            };
        }

        public Span<byte> GetArmatureParts(int partIndex)
        {
            var armature = GetArmature(partIndex);
            var offset = Unk0 + armature.offset;
            return GetSpan<byte>(offset, armature.count);
        }

        public Span<DataPoint> DataPoints
        {
            get
            {
                if (DataWidth != 80)
                    throw new InvalidOperationException("Invalid data width");

                var offset = DataOffset;
                var count = (_data.Length - offset) / DataWidth;
                return GetSpan<DataPoint>(offset, count);
            }
        }

        private Span<T> GetSpan<T>(int offset, int count) where T : struct
        {
            var data = new Span<byte>(_data, offset, _data.Length - offset);
            return MemoryMarshal.Cast<byte, T>(data).Slice(0, count);
        }

        [DebuggerDisplay("({x}, {y}, {z})")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vector
        {
            public short x, y, z;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Armature
        {
            public short count;
            public short offset;
        }

        [DebuggerDisplay("offset = {offset} speed = {speed}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct DataPoint
        {
            public Vector offset;
            public Vector speed;
            public fixed byte angles[68];
        }
    }
}
