using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    internal class MultiSet<T> : ICollection<T>, IEnumerable<T>
    {
        private readonly Dictionary<T, int> _dict = new Dictionary<T, int>();
        private int _count;

        public int Count => _count;
        public bool IsReadOnly => false;

        public void Add(T item)
        {
            _dict.TryGetValue(item, out var count);
            _dict[item] = count + 1;
            _count++;
        }

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        public bool Remove(T item)
        {
            if (_dict.TryGetValue(item, out var count))
            {
                if (count > 1)
                {
                    _dict[item] = count - 1;
                    _count--;
                    return true;
                }
                else
                {
                    _dict.Remove(item);
                    if (count == 1)
                    {
                        _count--;
                        return true;
                    }
                }
            }
            return false;
        }

        public int GetCount(T item)
        {
            _dict.TryGetValue(item, out var count);
            return count;
        }

        public void Clear()
        {
            _dict.Clear();
            _count = 0;
        }

        public bool Contains(T item) => GetCount(item) > 0;

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        bool ICollection<T>.Remove(T item) => Remove(item);

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var kvp in _dict)
            {
                for (var i = 0; i < kvp.Value; i++)
                {
                    yield return kvp.Key;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal class ImmutableMultiSet<T> : ICollection<T>, IEnumerable<T> where T : notnull
    {
        public static ImmutableMultiSet<Node> Empty { get; } = new ImmutableMultiSet<Node>();

        private readonly ImmutableDictionary<T, int> _dict = ImmutableDictionary<T, int>.Empty;

        public int Count { get; }
        public bool IsReadOnly => true;

        private ImmutableMultiSet()
        {
        }

        private ImmutableMultiSet(ImmutableDictionary<T, int> dict, int count)
        {
            _dict = dict;
            Count = count;
        }

        public ImmutableMultiSet<T> Add(T item)
        {
            _dict.TryGetValue(item, out var count);
            return new ImmutableMultiSet<T>(
                _dict.SetItem(item, count + 1),
                Count + 1);
        }

        public ImmutableMultiSet<T> AddRange(IEnumerable<T> items)
        {
            var result = this;
            foreach (var item in items)
            {
                result = result.Add(item);
            }
            return result;
        }

        public ImmutableMultiSet<T> Remove(T item)
        {
            if (_dict.TryGetValue(item, out var count))
            {
                if (count > 1)
                {
                    return new ImmutableMultiSet<T>(
                        _dict.SetItem(item, count - 1),
                        Count - 1);
                }
                else if (count == 1)
                {
                    return new ImmutableMultiSet<T>(
                        _dict.Remove(item),
                        Count - 1);
                }
            }
            return this;
        }

        public ImmutableMultiSet<T> RemoveMany(IEnumerable<T> items)
        {
            var result = this;
            foreach (var item in items)
            {
                result = result.Remove(item);
            }
            return result;
        }

        public int GetCount(T item)
        {
            _dict.TryGetValue(item, out var count);
            return count;
        }

        public bool Contains(T item) => GetCount(item) > 0;

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        void ICollection<T>.Add(T item) => throw new NotSupportedException();
        void ICollection<T>.Clear() => throw new NotSupportedException();
        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var kvp in _dict)
            {
                for (var i = 0; i < kvp.Value; i++)
                {
                    yield return kvp.Key;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
