using System;
using System.Collections.Generic;
using System.Linq;

namespace rer
{
    internal static class Extensions
    {
        public static T[] Shuffle<T>(this IEnumerable<T> items, Random random)
        {
            var array = items.ToArray();
            for (int i = 0; i < array.Length - 1; i++)
            {
                var ri = random.Next(i + 1, array.Length);
                var tmp = array[ri];
                array[ri] = array[i];
                array[i] = tmp;
            }
            return array;
        }

        public static T NextOf<T>(this Random random, params T[] values)
        {
            var i = random.Next(0, values.Length);
            return values[i];
        }

        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
        {
            return items.GroupBy(property).Select(x => x.First());
        }
    }
}
