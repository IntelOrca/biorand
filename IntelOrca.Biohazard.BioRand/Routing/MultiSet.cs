using System.Collections;
using System.Collections.Generic;

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
}
