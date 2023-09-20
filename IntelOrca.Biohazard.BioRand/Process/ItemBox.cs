using System;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Process
{
    public class ItemBox
    {
        private readonly ReItem[] _items;

        public Span<ReItem> Items => _items;

        public ItemBox(ReadOnlySpan<ReItem> items)
        {
            _items = items.ToArray();
        }

        public void Add(byte item, byte amount, bool combine)
        {
            if (combine)
            {
                for (var i = 0; i < _items.Length; i++)
                {
                    if (_items[i].Type == item)
                    {
                        var total = _items[i].Amount + amount;
                        if (total < 255)
                        {
                            _items[i].Amount = (byte)total;
                            return;
                        }
                    }
                }
            }

            var emptySlot = FindEmptyItemIndex();
            if (emptySlot == -1)
                return;

            _items[emptySlot].Type = item;
            _items[emptySlot].Amount = amount;
            _items[emptySlot].Size = 0;
            _items[emptySlot].zAlign = 0;
        }

        public int Count => _items.Count(x => x.Type != 0);

        private int FindEmptyItemIndex()
        {
            var firstItemIndex = FindFirstItemIndex();
            if (firstItemIndex == -1)
                return 0;
            for (var i = 0; i < _items.Length; i++)
            {
                var slot = (firstItemIndex + i) % _items.Length;
                if (_items[slot].Type == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindFirstItemIndex()
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (_items[i].Type != 0)
                {
                    return i;
                }
            }
            return -1;
        }

        public ItemBoxChange[] GetChangesFrom(ItemBox last)
        {
            var delta = last._items
                .GroupBy(x => x.Type)
                .ToDictionary(x => x.Key, x => -x.Sum(y => y.Amount));

            foreach (var item in _items)
            {
                if (delta.TryGetValue(item.Type, out var value))
                {
                    delta[item.Type] = value + item.Amount;
                }
                else
                {
                    delta[item.Type] = item.Amount;
                }
            }

            return delta
                .Where(x => x.Value != 0)
                .Select(x => new ItemBoxChange(x.Key, x.Value))
                .ToArray();
        }
    }

    public readonly struct ItemBoxChange
    {
        public byte Type { get; }
        public int Delta { get; }

        public ItemBoxChange(byte type, int delta)
        {
            Type = type;
            Delta = delta;
        }
    }
}
