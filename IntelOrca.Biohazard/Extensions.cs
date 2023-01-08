using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    internal static class Extensions
    {
        public static T[] Shuffle<T>(this IEnumerable<T> items, Rng rng)
        {
            var array = items.ToArray();
            for (int i = 0; i < array.Length - 1; i++)
            {
                var ri = rng.Next(i, array.Length);
                var tmp = array[ri];
                array[ri] = array[i];
                array[i] = tmp;
            }
            return array;
        }

        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
        {
            return items.GroupBy(property).Select(x => x.First());
        }

        public static IEnumerable<T> UnionExcept<T>(this IEnumerable<T> a, IEnumerable<T> b)
        {
            return a.Except(b).Union(b.Except(a));
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> items)
        {
            return new HashSet<T>(items);
        }

        public static Queue<T> ToQueue<T>(this IEnumerable<T> items)
        {
            return new Queue<T>(items);
        }

        public static EndlessBag<T> ToEndlessBag<T>(this IEnumerable<T> items, Rng rng)
        {
            return new EndlessBag<T>(rng, items);
        }

        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items)
        {
            foreach (var item in items)
                set.Add(item);
        }

        public static void RemoveMany<T>(this List<T> items, IEnumerable<T> removeList)
        {
            foreach (var item in removeList)
            {
                items.Remove(item);
            }
        }

        public static ulong CalculateFnv1a(this byte[] data)
        {
            var hash = 0x0CBF29CE484222325UL;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= 0x100000001B3UL;
            }
            return hash;
        }

        public static byte PeekByte(this BinaryReader br)
        {
            var pos = br.BaseStream.Position;
            var b = br.ReadByte();
            br.BaseStream.Position = pos;
            return b;
        }

        public static int ReadUInt24(this BinaryReader br)
        {
            var a = br.ReadUInt16();
            var b = br.ReadByte();
            return a | (b << 16);
        }

        public static void WriteASCII(this BinaryWriter bw, string s)
        {
            foreach (var c in s)
            {
                bw.Write((byte)c);
            }
        }

        public static unsafe T ReadStruct<T>(this BinaryReader br) where T : struct
        {
            var bytes = br.ReadBytes(Marshal.SizeOf<T>());
            fixed (byte* b = bytes)
            {
                return Marshal.PtrToStructure<T>((IntPtr)b);
            }
        }

        public static unsafe T Write<T>(this BinaryWriter bw, T structure) where T : struct
        {
            var result = default(T);
            var bytes = new byte[Marshal.SizeOf<T>()];
            fixed (byte* b = bytes)
            {
                Marshal.StructureToPtr(structure, (IntPtr)b, false);
            }
            bw.Write(bytes);
            return result;
        }

        public static void CopyAmountTo(this Stream source, Stream destination, long length)
        {
            var buffer = new byte[4096];
            long count = 0;
            while (count < length)
            {
                var readLen = (int)Math.Min(buffer.Length, length - count);
                var read = source.Read(buffer, 0, readLen);
                if (read == 0)
                    break;

                destination.Write(buffer, 0, read);
                count += read;
            }
            if (count != length)
                throw new IOException($"Unable to copy {length} bytes to new stream.");
        }

        public static ForkedBinaryReader Fork(this BinaryReader br)
        {
            return new ForkedBinaryReader(br);
        }
    }
}
