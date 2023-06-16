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

        public ushort ArmatureOffset => BitConverter.ToUInt16(_data, 0);
        public ushort KeyFrameOffset => BitConverter.ToUInt16(_data, 2);
        public ushort NumParts => BitConverter.ToUInt16(_data, 4);
        public ushort KeyFrameSize => BitConverter.ToUInt16(_data, 6);

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

            var offset = ArmatureOffset + (partIndex * 4);
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
            var offset = ArmatureOffset + armature.offset;
            return GetSpan<byte>(offset, armature.count);
        }

        public Span<byte> KeyFrameData
        {
            get
            {
                var offset = KeyFrameOffset;
                var count = _data.Length - offset;
                return GetSpan<byte>(offset, count);
            }
        }

        public Span<KeyFrame> KeyFrames
        {
            get
            {
                if (KeyFrameSize != 80)
                    throw new InvalidOperationException("Invalid data width");

                var offset = KeyFrameOffset;
                var count = (_data.Length - offset) / KeyFrameSize;
                return GetSpan<KeyFrame>(offset, count);
            }
        }

        private Span<T> GetSpan<T>(int offset, int count) where T : struct
        {
            var data = new Span<byte>(_data, offset, _data.Length - offset);
            return MemoryMarshal.Cast<byte, T>(data).Slice(0, count);
        }

        public EmrBuilder ToBuilder()
        {
            var builder = new EmrBuilder();
            var numParts = NumParts;
            for (var i = 0; i < numParts; i++)
            {
                builder.RelativePositions.Add(GetRelativePosition(i));
            }
            for (var i = 0; i < numParts; i++)
            {
                builder.Armatures.Add(GetArmatureParts(i).ToArray());
            }
            builder.KeyFrameData = KeyFrameData.ToArray();
            builder.KeyFrameSize = KeyFrameSize;
            return builder;
        }

        public Emr WithKeyframes(Emr emr)
        {
            var builder = ToBuilder();
            builder.KeyFrameSize = emr.KeyFrameSize;
            builder.KeyFrameData = emr.KeyFrameData.ToArray();
            return builder.ToEmr();
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
        public unsafe struct KeyFrame
        {
            public Vector offset;
            public Vector speed;
            public fixed byte angles[68];

            public Vector GetAngle(int i)
            {
                fixed (byte* ptr = angles)
                {
                    var nibble = i * 9;
                    var byteIndex = nibble / 2;
                    var src = ptr + byteIndex;

                    // Read 3 nibbles
                    var x = ReadAngle(ref src, ref nibble);
                    var y = ReadAngle(ref src, ref nibble);
                    var z = ReadAngle(ref src, ref nibble);

                    // var x = Read12(ptr, 0);
                    // var y = Read12(ptr, 1);
                    // var z = Read12(ptr, 2);
                    return new Vector()
                    {
                        x = x,
                        y = y,
                        z = z
                    };
                }
            }

            private static short ReadAngle(ref byte* src, ref int nibble)
            {
                var a = ReadNibble(ref src, ref nibble);
                var b = ReadNibble(ref src, ref nibble);
                var c = ReadNibble(ref src, ref nibble);
                return (short)((c << 8) | (b << 4) | a);
            }

            private static byte ReadNibble(ref byte* src, ref int nibble)
            {
                byte value;
                if ((nibble & 1) == 0)
                {
                    value = (byte)(*src & 0x0F);
                }
                else
                {
                    value = (byte)(*src >> 4);
                    src++;
                }
                nibble++;
                return value;
            }

            private static short Read12(byte* array, int index)
            {
                short val = 0;
                switch (index & 1)
                {
                    case 0: /* XX and -X */
                        val = (short)(array[index] | (array[index + 1] << 8));
                        break;
                    case 1: /* Y- and YY */
                        val = (short)((array[index] >> 4) | (array[index + 1] << 4));
                        break;
                }
                val &= 0xFFF;
                if ((val & (1 << 11)) != 0)
                {
                    val = (short)(val | 0xF000);
                }

                return val;
            }
        }
    }
}
