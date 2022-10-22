using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace rer
{
    internal class PlayGraphFactory
    {
        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Map _map = new Map();
        private Dictionary<RdtId, PlayNode> _nodes = new Dictionary<RdtId, PlayNode>();
        private List<ItemPoolEntry> _currentPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _shufflePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private List<(RdtItemId, RdtItemId)> _linkedItems = new List<(RdtItemId, RdtItemId)>();
        private HashSet<ushort> _requiredItems = new HashSet<ushort>();
        private HashSet<ushort> _haveItems = new HashSet<ushort>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private bool _firstRedJewelPlaced;
        private Rng _rng;

        public PlayGraphFactory(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _map = map;
            _rng = random;
        }

        public void CreateDoorRando()
        {
            var nodes = new List<PlayNode>();
            foreach (var kvp in _map.Rooms!)
            {
                nodes.Add(GetOrCreateNode(RdtId.Parse(kvp.Key)));
            }

            foreach (var node in nodes)
            {
                var rdt = _gameData.GetRdt(node.RdtId);
                if (rdt != null)
                {
                    foreach (var door in rdt.Doors)
                    {
                        // Get a random door to go to
                        PlayNode targetNode;
                        do
                        {
                            var nodeIndex = _rng.Next(0, nodes.Count);
                            targetNode = nodes[nodeIndex];
                        }
                        while (targetNode.Doors.Length == 0);
                        var doorIndex = _rng.Next(0, targetNode.Doors.Length);
                        var targetDoor = targetNode.Doors[doorIndex];

                        rdt.SetDoorTarget(door.Id, targetDoor);
                    }
                }
            }
            foreach (var rdt in _gameData.Rdts)
            {
                rdt.Save();
            }
        }

        public PlayGraph Create()
        {
            _logger.WriteHeading("Randomizing Items:");
            _logger.WriteLine("Placing key items:");

            var graph = new PlayGraph();
            graph.Start = GetOrCreateNode(RdtId.Parse(_map.Start!));
            graph.End = GetOrCreateNode(RdtId.Parse(_map.End!));

            var checkpoint = graph.Start;
            while (!_visitedRooms.Contains(graph.End))
            {
                PlaceKeyItem();
                var newCheckpoint = Search(checkpoint);
                if (newCheckpoint != checkpoint)
                {
                    _logger.WriteLine("    ------------ checkpoint ------------");
                    if (_config.ProtectFromSoftLock)
                    {
                        _shufflePool.AddRange(_currentPool);
                        _currentPool.Clear();
                    }
                }
                checkpoint = newCheckpoint;
            }
            _shufflePool.AddRange(_currentPool);

            if (_config.ShuffleItems)
            {
                ShuffleRemainingPool();
            }
            else
            {
                RandomiseRemainingPool();
            }

            SetLinkedItems();
            return graph;
        }

        private static Map LoadJsonMap(string path)
        {
            var jsonMap = File.ReadAllText(path);
            var map = JsonSerializer.Deserialize<Map>(jsonMap, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
            return map;
        }

        private PlayNode Search(PlayNode start)
        {
            var checkpoint = start;
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
                    // Add any required keys for the room (ones that don't guard an item, e.g. Film A, Film B, etc.)
                    foreach (var r in node.Requires)
                    {
                        if (!_haveItems.Contains(r))
                        {
                            _requiredItems.Add(r);
                        }
                    }

                    // First time we have visited room, add room items to pool
                    _currentPool.AddRange(node.Items);
                    foreach (var linkedItem in node.LinkedItems)
                    {
                        _linkedItems.Add((new RdtItemId(node.RdtId, linkedItem.Key), linkedItem.Value));
                    }

                    if (node.Items.Length != 0)
                    {
                        // _logger.WriteLine($"    Room {node.RdtId} contains:");
                        foreach (var item in node.Items)
                        {
                            // _logger.WriteLine($"        {item}");
                            if (item.Requires != null)
                            {
                                foreach (var r in item.Requires)
                                {
                                    if (!_haveItems.Contains(r))
                                    {
                                        _requiredItems.Add(r);
                                    }
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
                    if (edge.NoReturn && _requiredItems.Count != 0)
                        continue;

                    var requiredItems = edge.Requires == null ? new ushort[0] : edge.Requires.Except(_haveItems).ToArray()!;
                    if (requiredItems.Length == 0)
                    {
                        if (edge.NoReturn)
                        {
                            checkpoint = edge.Node;
                            // _logger.WriteLine($"        {node} -> {edge.Node} (checkpoint)");
                        }
                        else
                        {
                            // _logger.WriteLine($"        {node} -> {edge.Node}");
                            stack.Push(edge.Node);
                        }
                    }
                    else
                    {
                        foreach (var item in requiredItems)
                        {
                            if (!_haveItems.Contains(item))
                            {
                                _requiredItems.Add(item);
                            }
                        }
                    }
                }
            }
            return checkpoint;
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
            var randomOrder = Enumerable.Range(0, _currentPool.Count).Shuffle(_rng).ToArray();
            foreach (var i in randomOrder)
            {
                var item = _currentPool[i];
                if (item.Type != type && item.Priority != ItemPriority.Low && HasAllRequiredItems(item.Requires))
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

            var checkList = _requiredItems.Shuffle(_rng);
            foreach (var req in checkList)
            {
                if (PlaceKeyItem(req))
                {
                    return;
                }
            }

            _logger.WriteLine("    Unable to place the following key items:");
            foreach (var item in checkList)
            {
                _logger.WriteLine($"        {Items.GetItemName(item)}");
            }

            throw new Exception("Unable to find key item to swap");
        }

        private bool PlaceKeyItem(ushort req)
        {
            var swapA = default(ItemPoolEntry);
            var swapB = default(ItemPoolEntry);

            // Get a new location for the key item
            var index = FindNewKeyItemLocation(req);
            var itemEntry = _currentPool[index];

            // Find original location of key item
            var originalIndex = _currentPool.FindIndex(x => x.Type == req);
            if (originalIndex != -1)
            {
                // Change original key item to the item we are going to replace
                var originalItemEntry = _currentPool[originalIndex];
                swapA = originalItemEntry;
                swapB = itemEntry;
                var keyCount = originalItemEntry.Amount;
                originalItemEntry.Type = itemEntry.Type;
                originalItemEntry.Amount = itemEntry.Amount;
                _currentPool[originalIndex] = originalItemEntry;
                itemEntry.Amount = keyCount;
            }
            else
            {
                return false;
            }
            itemEntry.Type = req;

            // Remove new key item location from pool
            if (req == 0x33 && !_firstRedJewelPlaced) // red jewel
            {
                _firstRedJewelPlaced = true;
            }
            else
            {
                _requiredItems.Remove(req);
            }
            _haveItems.Add(req);
            _currentPool.RemoveAt(index);
            _definedPool.Add(itemEntry);
            _logger.WriteLine($"    Placing key item ({Items.GetItemName(itemEntry.Type)}) in {itemEntry.RdtId}:{itemEntry.Id}");
            // _logger.WriteLine($"        Swapped {swapA} with {swapB}");
            return true;
        }

        private void RandomiseRemainingPool()
        {
            _logger.WriteLine("Randomizing non-key items:");

            double ammo = _config.RatioAmmo / 32.0;
            double health = _config.RatioHealth / 32.0;
            double ink = _config.RatioInkRibbons / 32.0;

            var table = _rng.CreateProbabilityTable<ItemType>();
            table.Add(ItemType.InkRibbon, ink);

            table.Add(ItemType.HerbG, health * 0.5);
            table.Add(ItemType.HerbR, health * 0.3);
            table.Add(ItemType.HerbB, health * 0.1);
            table.Add(ItemType.FAidSpray, health * 0.1);

            table.Add(ItemType.HandgunAmmo, ammo * 0.4);
            table.Add(ItemType.BowgunAmmo, ammo * 0.1);
            table.Add(ItemType.GrenadeRounds, ammo * 0.1);
            table.Add(ItemType.AcidRounds, ammo * 0.1);
            table.Add(ItemType.FlameRounds, ammo * 0.1);
            table.Add(ItemType.SparkshotAmmo, ammo * 0.1);
            table.Add(ItemType.SMGammo, ammo * 0.1);

            for (int i = 0; i < _shufflePool.Count; i++)
            {
                var itemType = table.Next();

                var entry = _shufflePool[i];
                entry.Type = (byte)itemType;
                entry.Amount = GetRandomAmount(itemType);
                _logger.WriteLine($"    Replaced {_shufflePool[i]} with {entry}");
                _definedPool.Add(entry);
            }
            _shufflePool.Clear();
        }

        private byte GetRandomAmount(ItemType type)
        {
            var multiplier = _config.AmmoQuantity / 8.0;
            switch (type)
            {
                default:
                    return 1;
                case ItemType.InkRibbon:
                    return (byte)_rng.Next(1, 3);
                case ItemType.HandgunAmmo:
                    return (byte)_rng.Next(1, (int)(60 * multiplier));
                case ItemType.BowgunAmmo:
                    return (byte)_rng.Next(1, (int)(30 * multiplier));
                case ItemType.GrenadeRounds:
                case ItemType.AcidRounds:
                case ItemType.FlameRounds:
                    return (byte)_rng.Next(1, (int)(10 * multiplier));
                case ItemType.SparkshotAmmo:
                case ItemType.SMGammo:
                    return (byte)_rng.Next(1, (int)(100 * multiplier));
            }
        }

        private void ShuffleRemainingPool()
        {
            _logger.WriteLine("Shuffling non-key items:");
            var shuffled = _shufflePool.Shuffle(_rng);
            for (int i = 0; i < _shufflePool.Count; i++)
            {
                var entry = _shufflePool[i];
                entry.Type = shuffled[i].Type;
                entry.Amount = shuffled[i].Amount;
                _logger.WriteLine($"    Swapped {_shufflePool[i]} with {shuffled[i]}");
                _definedPool.Add(entry);
            }
            _shufflePool.Clear();
        }

        private void SetLinkedItems()
        {
            _logger.WriteLine("Setting up linked items:");
            foreach (var (targetId, sourceId) in _linkedItems)
            {
                var sourceItem = _definedPool.Find(x => x.RdtItemId == sourceId);
                var targetItem = sourceItem;
                targetItem.RdtItemId = targetId;
                _definedPool.Add(targetItem);
                _logger.WriteLine($"    {sourceItem} placed at {targetId}");
            }
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
            node.Doors = GetAllDoorsToRoom(rdtId);
            node.Items = items;
            _nodes.Add(rdtId, node);

            var mapRoom = _map.GetRoom(rdtId);
            if (mapRoom != null)
            {
                node.Requires = mapRoom.Requires ?? Array.Empty<ushort>();

                if (mapRoom.Doors != null)
                {
                    foreach (var door in mapRoom.Doors)
                    {
                        var edgeNode = GetOrCreateNode(RdtId.Parse(door.Target!));
                        var edge = new PlayEdge(edgeNode, door.Locked, door.NoReturn, door.Requires!);
                        node.Edges.Add(edge);
                    }
                }

                if (mapRoom.Items != null)
                {
                    foreach (var fixedItem in mapRoom.Items)
                    {
                        var idx = Array.FindIndex(items, x => x.Id == fixedItem.Id);
                        if (idx != -1)
                        {
                            if (fixedItem.Link == null)
                            {
                                items[idx].Type = (ushort)fixedItem.Type;
                                items[idx].Amount = fixedItem.Amount ?? items[idx].Amount;
                            }
                            else
                            {
                                items[idx].Type = 0;

                                var rdtItemId = RdtItemId.Parse(fixedItem.Link);
                                node.LinkedItems.Add(fixedItem.Id, rdtItemId);
                            }
                            items[idx].Requires = fixedItem.Requires;
                            if (fixedItem.Priority != null)
                            {
                                items[idx].Priority = (ItemPriority)Enum.Parse(typeof(ItemPriority), fixedItem.Priority, true);
                            }
                        }
                    }

                    // Remove any items that have no type (removed fixed items)
                    node.Items = node.Items.Where(x => x.Type != 0).ToArray();
                }
            }
            return node;
        }

        public PlayNode? FindNode(RdtId rdtId)
        {
            if (_nodes.TryGetValue(rdtId, out var node))
                return node;
            return null;
        }

        private Door[] GetAllDoorsToRoom(RdtId room)
        {
            var doors = new List<Door>();
            foreach (var rdt in _gameData.Rdts)
            {
                foreach (var door in rdt.Doors)
                {
                    if (door.Stage == room.Stage && door.Room == room.Room)
                    {
                        if (!doors.Any(x => IsDoorTheSame(x, door)))
                        {
                            doors.Add(door);
                        }
                    }
                }
            }
            return doors.ToArray();
        }

        private static bool IsDoorTheSame(Door a, Door b)
        {
            if (a.Camera != b.Camera)
                return false;

            var dx = a.NextX - b.NextX;
            var dy = a.NextY - b.NextY;
            var dz = a.NextZ - b.NextZ;
            var d = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            return d < 1024;
        }
    }
}
