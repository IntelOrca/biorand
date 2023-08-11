using System;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Cli
{
    internal class ItemBox
    {
        private readonly ReItem[] _items;

        public Span<ReItem> Items => _items;

        public ItemBox(ReadOnlySpan<ReItem> items)
        {
            _items = items.ToArray();
        }

        public void Add(byte item, byte amount)
        {
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
    }
}