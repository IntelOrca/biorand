using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace rer
{
    internal class PlayGraphFactory
    {
        private GameData _gameData;
        private Map _map = new Map();
        private Dictionary<RdtId, PlayNode> _nodes = new Dictionary<RdtId, PlayNode>();
        private List<ItemPoolEntry> _itemPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private HashSet<ushort> _requiredItems = new HashSet<ushort>();
        private HashSet<ushort> _haveItems = new HashSet<ushort>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private Random _random = new Random(0);

        public PlayGraph Create(GameData gameData, string path)
        {
            _gameData = gameData;

            var jsonMap = File.ReadAllText(path);
            _map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

            var graph = new PlayGraph();
            graph.Start = GetOrCreateNode(new RdtId(_map.Start!.Stage, _map.Start!.Room));
            graph.End = GetOrCreateNode(new RdtId(_map.End!.Stage, _map.End!.Room));

            Search(graph.Start);
            while (_requiredItems.Count != 0)
            {
                PlaceKeyItem();
                Search(graph.Start);
            }
            RandomiseRemainingPool();
            return graph;
        }

        private void Search(PlayNode start)
        {
            var seen = new HashSet<RdtId>();
            var walkedNodes = new List<PlayNode>();
            var stack = new Stack<PlayNode>();
            stack.Push(start);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                walkedNodes.Add(node);
                seen.Add(node.RdtId);

                if (_visitedRooms.Add(node))
                {
                    // First time we have visited room, add room items to pool
                    _itemPool.AddRange(node.Items!);

                    if (node.Items != null && node.Items.Length != 0)
                    {
                        Console.WriteLine($"Room {node.RdtId} contains:");
                        foreach (var item in node.Items)
                        {
                            Console.WriteLine($"    {item}");
                            if (item.Requires != null)
                            {
                                foreach (var r in item.Requires)
                                {
                                    _requiredItems.Add(r);
                                }
                            }
                        }
                    }
                }

                foreach (var edge in node.Edges)
                {
                    if (seen.Contains(edge.Node.RdtId))
                        continue;
                    if (edge.Locked)
                        continue;

                    var requiredItems = edge.Requires.Except(_haveItems).ToArray()!;
                    if (requiredItems.Length == 0)
                    {
                        // Console.WriteLine($"{node} -> {edge.Node}");
                        stack.Push(edge.Node);
                    }
                    else
                    {
                        foreach (var item in requiredItems)
                        {
                            _requiredItems.Add(item);
                        }
                    }
                }
            }
        }

        private bool HasAllRequiredItems(ushort[]? items)
        {
            if (items != null && items.Length != 0)
            {
                foreach (var r in items)
                {
                    if (!_haveItems.Contains(r))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private int FindNewKeyItemLocation(int type)
        {
            var randomOrder = Enumerable.Range(0, _itemPool.Count).Shuffle(_random).ToArray();
            foreach (var i in randomOrder)
            {
                if (_itemPool[i].Type != type && HasAllRequiredItems(_itemPool[i].Requires))
                {
                    return i;
                }
            }
            return 0;
        }

        private void PlaceKeyItem()
        {
            if (_requiredItems.Count == 0)
                return;

            var checkList = _requiredItems.Shuffle(_random);
            foreach (var req in checkList)
            {
                if (PlaceKeyItem(req))
                {
                    return;
                }
            }

            Console.WriteLine("Unable to place the following key items:");
            foreach (var item in checkList)
            {
                Console.WriteLine($"    {Items.GetItemName(item)}");
            }

            throw new Exception("Unable to find key item to swap");
        }

        private bool PlaceKeyItem(ushort req)
        {
            var swapA = default(ItemPoolEntry);
            var swapB = default(ItemPoolEntry);

            // Get a new location for the key item
            var index = FindNewKeyItemLocation(req);
            var itemEntry = _itemPool[index];

            // Find original location of key item
            var originalIndex = _itemPool.FindIndex(x => x.Type == req);
            if (originalIndex != -1)
            {
                // Change original key item to the item we are going to replace
                var originalItemEntry = _itemPool[originalIndex];
                swapA = originalItemEntry;
                swapB = itemEntry;
                var keyCount = originalItemEntry.Amount;
                originalItemEntry.Type = itemEntry.Type;
                originalItemEntry.Amount = itemEntry.Amount;
                _itemPool[originalIndex] = originalItemEntry;
                itemEntry.Amount = keyCount;
            }
            else
            {
                return false;
            }
            itemEntry.Type = req;

            // Remove new key item location from pool
            _requiredItems.Remove(req);
            _haveItems.Add(req);
            _itemPool.RemoveAt(index);
            _definedPool.Add(itemEntry);
            Console.WriteLine($"Placing key item ({Items.GetItemName(itemEntry.Type)}) in {itemEntry.RdtId}:{itemEntry.Id}");
            Console.WriteLine($"    Swapped {swapA} with {swapB}");
            return true;
        }

        private void RandomiseRemainingPool()
        {
            Console.WriteLine("Shuffling non-key items:");
            var shuffled = _itemPool.Shuffle(_random);
            for (int i = 0; i < _itemPool.Count; i++)
            {
                var entry = _itemPool[i];
                entry.Type = shuffled[i].Type;
                entry.Amount = shuffled[i].Amount;
                Console.WriteLine($"    Swapped {_itemPool[i]} with {shuffled[i]}");
                _definedPool.Add(entry);
            }
            _itemPool.Clear();
        }

        public void Save()
        {
            foreach (var entry in _definedPool)
            {
                var rdt = _gameData.GetRdt(entry.RdtId)!;
                rdt.SetItem(entry.Id, entry.Type, entry.Amount);
                rdt.Save();
            }
        }

        private PlayNode GetOrCreateNode(RdtId rdtId)
        {
            var node = FindNode(rdtId);
            if (node != null)
                return node;

            var rdt = _gameData.GetRdt(rdtId);
            var items = rdt!.Items.Select(x => new ItemPoolEntry()
            {
                RdtId = rdt.RdtId,
                Id = x.Id,
                Type = x.Type,
                Amount = x.Amount
            }).DistinctBy(x => x.Id).Where(x => x.Type < 0x64).ToArray();

            node = new PlayNode(rdtId);
            node.Items = items;
            _nodes.Add(rdtId, node);

            var mapRoom = _map.GetRoom(rdtId.Stage, rdtId.Room);
            foreach (var door in mapRoom!.Doors!)
            {
                var edgeNode = GetOrCreateNode(new RdtId(door.Stage, door.Room));
                var edge = new PlayEdge(edgeNode, door.Locked, door.Requires!);
                node.Edges.Add(edge);
            }

            if (mapRoom.Items != null)
            {
                foreach (var fixedItem in mapRoom.Items)
                {
                    var idx = Array.FindIndex(items, x => x.Id == fixedItem.Id);
                    if (idx != -1)
                    {
                        items[idx].Type = (ushort)fixedItem.Type;
                        items[idx].Requires = fixedItem.Requires;
                    }
                }

                // Remove any items that have no type (removed fixed items)
                node.Items = node.Items.Where(x => x.Type != 0).ToArray();
            }

            return node;
        }

        public PlayNode? FindNode(RdtId rdtId)
        {
            if (_nodes.TryGetValue(rdtId, out var node))
                return node;
            return null;
        }
    }
}
