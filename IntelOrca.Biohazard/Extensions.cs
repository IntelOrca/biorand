using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard
{
    internal static class Extensions
    {
        public static T[] Shuffle<T>(this IEnumerable<T> items, Rng rng)
        {
            var array = items.ToArray();
            for (int i = 0; i < array.Length - 1; i++)
            {
                var ri = rng.Next(i + 1, array.Length);
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

        public static Queue<T> ToQueue<T>(this IEnumerable<T> items)
        {
            return new Queue<T>(items);
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

        public static void WriteASCII(this BinaryWriter bw, string s)
        {
            foreach (var c in s)
            {
                bw.Write((byte)c);
            }
        }
    }
}
