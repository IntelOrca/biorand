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
            for (int i = 0; i < array.Length; i++)
            {
                var ri = random.Next(0, array.Length);
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
    }
}
