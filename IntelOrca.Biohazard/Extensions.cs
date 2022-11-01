using System;
using System.Collections.Generic;
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
    }
}
