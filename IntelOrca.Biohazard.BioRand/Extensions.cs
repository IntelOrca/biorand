using System;
using System.Collections.Generic;
using System.Linq;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.BioRand
{
    public static class Extensions2
    {
        public static string ToTitle(this string x)
        {
            var chars = x.ToCharArray();
            var newWord = true;
            for (int i = 0; i < chars.Length; i++)
            {
                if (newWord && char.IsLetter(chars[i]))
                {
                    chars[i] = char.ToUpper(chars[i]);
                    newWord = false;
                }
                else if (!char.IsLetter(chars[i]))
                {
                    newWord = true;
                }
            }
            return new string(chars);
        }

        public static string GetBaseName(this string x) => GetBaseName(x, '.');

        public static string GetBaseName(this string x, char delimiter)
        {
            var fsIndex = x.IndexOf(delimiter);
            if (fsIndex != -1)
                return x.Substring(0, fsIndex);
            return x;
        }

        public static string StripActorSkin(this string x)
        {
            var skinIndex = x.IndexOf('$');
            if (skinIndex != -1)
            {
                return x.Substring(0, skinIndex);
            }
            return x;
        }

        public static string? GetActorSkin(this string x)
        {
            var skinIndex = x.IndexOf('$');
            if (skinIndex != -1)
            {
                return x.Substring(skinIndex + 1);
            }
            return null;
        }

        public static string ToActorString(this string x)
        {
            var actor = StripActorSkin(x);
            var fsIndex = actor.IndexOf('.');
            if (fsIndex != -1)
            {
                var name = actor.Substring(0, fsIndex).ToTitle();
                var game = actor.Substring(fsIndex + 1).ToUpper();
                actor = $"{name} ({game})";
            }
            else
            {
                actor = actor.ToTitle();
            }

            var skin = GetActorSkin(x);
            if (skin != null)
            {
                actor += $" [{skin}]";
            }
            return actor;
        }

        public static bool IsSherryActor(this string? actor)
        {
            var sherry = "sherry";
            if (actor == null)
                return false;

            var fsIndex = actor.IndexOf('.');
            if (fsIndex != -1)
            {
                if (actor.Length - fsIndex + 1 != sherry.Length)
                    return false;
                return actor.StartsWith(sherry, StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(actor, sherry, StringComparison.OrdinalIgnoreCase);
        }

        public static T Random<T>(this IEnumerable<T> items, Rng rng)
        {
            var index = rng.Next(0, items.Count());
            return items.ElementAt(index);
        }

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

        internal static EndlessBag<T> ToEndlessBag<T>(this IEnumerable<T> items, Rng rng)
        {
            return new EndlessBag<T>(rng, items);
        }

        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items)
        {
            foreach (var item in items)
                set.Add(item);
        }

        public static void RemoveMany<T>(this ICollection<T> items, IEnumerable<T> removeList)
        {
            foreach (var item in removeList)
            {
                items.Remove(item);
            }
        }

        public static IEnumerable<T> EnumerateOpcodes<T>(this RandomizedRdt rdt, RandoConfig config) => AstEnumerator<T>.Enumerate(rdt.Ast!, config);

        public static short GetHeight(this ModelFile model)
        {
            return model.GetEmr(0).GetRelativePosition(0).y;
        }

        public static double CalculateEmrScale(this ModelFile newModel, ModelFile originalModel)
        {
            var sourceHeight = originalModel.GetHeight();
            var targetHeight = newModel.GetHeight();
            return GetScale(sourceHeight, targetHeight);
        }

        public static double GetScale(short sourceHeight, short targetHeight)
        {
            var result = ((double)targetHeight / sourceHeight) + 0.03;

            // Don't bother scaling if only slightly out
            // if (Math.Abs(result - 1) <= 0.2)
            //     return 1;

            return result;
        }

        public static bool Implements(this Type t, Type i)
        {
            return t.GetInterfaces().Contains(i);
        }
    }
}
