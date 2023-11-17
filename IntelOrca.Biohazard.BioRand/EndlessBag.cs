using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand
{
    internal class EndlessBag<T>
    {
        private readonly Rng _rng;
        private readonly List<T> _allItems = new List<T>();
        private readonly Queue<T> _items = new Queue<T>();

        public int Count => _allItems.Count;

        public EndlessBag(Rng rng, IEnumerable<T> items)
        {
            _rng = rng;
            _allItems.AddRange(items);
        }

        public T Next()
        {
            if (_allItems.Count == 0)
                throw new Exception("No items in bag.");

            if (_items.Count == 0)
            {
                var toAdd = _allItems.Shuffle(_rng);
                foreach (var item in toAdd)
                {
                    _items.Enqueue(item);
                }
            }
            return _items.Dequeue();
        }

        public T[] Next(int count)
        {
            var result = new T[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = Next();
            }
            return result;
        }
    }
}
