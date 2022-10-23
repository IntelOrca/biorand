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
        private List<PlayNode> _nodes = new List<PlayNode>();
        private Dictionary<RdtId, PlayNode> _nodeMap = new Dictionary<RdtId, PlayNode>();
        private List<ItemPoolEntry> _futurePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _currentPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _shufflePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private List<(RdtItemId, RdtItemId)> _linkedItems = new List<(RdtItemId, RdtItemId)>();
        private HashSet<ushort> _requiredItems = new HashSet<ushort>();
        private HashSet<ushort> _haveItems = new HashSet<ushort>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private bool _firstRedJewelPlaced;
        private Rng _rng;
        private bool _debugLogging;

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

            // Create all nodes
            foreach (var kvp in _map.Rooms!)
            {
                _nodes.Add(GetOrCreateNode(RdtId.Parse(kvp.Key)));
            }

            var graph = new PlayGraph();
            if (_config.Scenario == 0)
            {
                graph.Start = GetOrCreateNode(RdtId.Parse(_map.StartA!));
                graph.End = GetOrCreateNode(RdtId.Parse(_map.EndA!));
            }
            else
            {
                graph.Start = GetOrCreateNode(RdtId.Parse(_map.StartB!));
                graph.End = GetOrCreateNode(RdtId.Parse(_map.EndB!));
            }

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
                    foreach (var item in node.Items)
                    {
                        // Check if we have already added this item in advance
                        var futureIndex = _futurePool.FindIndex(x => x.RdtItemId == item.RdtItemId);
                        if (futureIndex == -1)
                        {
                            _currentPool.Add(item);
                        }
                        else
                        {
                            _futurePool.RemoveAt(futureIndex);
                        }
                    }
                    foreach (var linkedItem in node.LinkedItems)
                    {
                        _linkedItems.Add((new RdtItemId(node.RdtId, linkedItem.Key), linkedItem.Value));
                    }

                    if (node.Items.Length != 0)
                    {
                        if (_debugLogging)
                        {
                            _logger.WriteLine($"    Room {node.RdtId} contains:");
                        }
                        foreach (var item in node.Items)
                        {
                            if (_debugLogging)
                            {
                                _logger.WriteLine($"        {item}");
                            }
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
                            if (_debugLogging)
                            {
                                _logger.WriteLine($"        {node} -> {edge.Node} (checkpoint)");
                            }
                        }
                        else
                        {
                            if (_debugLogging)
                            {
                                _logger.WriteLine($"        {node} -> {edge.Node}");
                            }
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

        private int? FindNewKeyItemLocation(int type)
        {
            var randomOrder = Enumerable.Range(0, _currentPool.Count).Shuffle(_rng).ToArray();
            var bestI = (int?)null;
            foreach (var i in randomOrder)
            {
                var item = _currentPool[i];
                if (item.Priority == ItemPriority.Normal && HasAllRequiredItems(item.Requires))
                {
                    if (item.Type == type)
                        bestI = i;
                    else
                        return i;
                }
            }
            return bestI;
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

            _requiredItems.Clear();
            // throw new Exception("Unable to find key item to swap");
        }

        private bool PlaceKeyItem(ushort req)
        {
            // Get a new location for the key item
            var index = FindNewKeyItemLocation(req);
            if (index == null)
                throw new Exception("Run out of free item slots");

            var itemEntry = _currentPool[index.Value];

            // Find original location of key item
            var originalIndex = _currentPool.FindIndex(x => x.Type == req);
            if (originalIndex == -1 && _config.AlternativeRoutes)
            {
                // Key must be in a layer area, find it
                foreach (var node in _nodes)
                {
                    var futureItem = node.Items.FirstOrDefault(x => x.Type == req);
                    if (futureItem.Type == req && futureItem.Priority != ItemPriority.Fixed)
                    {
                        _futurePool.Add(futureItem);
                        _currentPool.Add(futureItem);
                        originalIndex = _currentPool.Count - 1;
                        break;
                    }
                }
            }
            if (originalIndex != -1)
            {
                // Check original key can actually be moved
                var originalItemEntry = _currentPool[originalIndex];
                if (originalItemEntry.Priority == ItemPriority.Fixed)
                {
                    index = originalIndex;
                    itemEntry = originalItemEntry;
                }

                // Change original key item to the item we are going to replace
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
            _currentPool.RemoveAt(index.Value);
            _definedPool.Add(itemEntry);
            _logger.WriteLine($"    Placing key item ({Items.GetItemName(itemEntry.Type)}) in {itemEntry.RdtId}:{itemEntry.Id}");
            return true;
        }

        private void RandomiseRemainingPool()
        {
            _logger.WriteLine("Randomizing non-key items:");

            // Shuffle the pool, keep low priority at the end
            var shuffled = _shufflePool
                .Where(x => x.Priority == ItemPriority.Normal)
                .Shuffle(_rng)
                .Concat(_shufflePool.Where(x => x.Priority != ItemPriority.Normal))
                .ToQueue();
            _shufflePool.Clear();

            // Weapons first
            var ammoTypes = new HashSet<ItemType>() { ItemType.HandgunAmmo };
            var items = new List<ItemType>();
            if (_rng.Next(0, 3) >= 1)
                items.Add(ItemType.Bowgun);
            if (_rng.Next(0, 3) >= 1)
                items.Add(_rng.NextOf(ItemType.GrenadeLauncherExplosive, ItemType.GrenadeLauncherFlame, ItemType.GrenadeLauncherAcid));
            if (_rng.Next(0, 2) == 0)
                items.Add(ItemType.SMG);
            if (_rng.Next(0, 2) == 0)
                items.Add(ItemType.Sparkshot);
            if (_rng.Next(0, 2) == 0)
                items.Add(ItemType.ColtSAA);
            if (_rng.Next(0, 2) == 0)
                items.Add(ItemType.RocketLauncher);
            foreach (var itemType in items)
            {
                var ammoType = GetAmmoTypeForWeapon(itemType);
                var amount = GetRandomAmount(ammoType);
                SpawnItem(shuffled, itemType, amount);
                ammoTypes.Add(ammoType);
            }

            // Now everything else
            double ammo = _config.RatioAmmo / 32.0;
            double health = _config.RatioHealth / 32.0;
            double ink = _config.RatioInkRibbons / 32.0;

            var table = _rng.CreateProbabilityTable<ItemType>();
            table.Add(ItemType.InkRibbon, ink);

            table.Add(ItemType.HerbG, health * 0.5);
            table.Add(ItemType.HerbR, health * 0.3);
            table.Add(ItemType.HerbB, health * 0.1);
            table.Add(ItemType.FAidSpray, health * 0.1);

            if (ammoTypes.Contains(ItemType.HandgunAmmo))
                table.Add(ItemType.HandgunAmmo, ammo * 0.4);
            if (ammoTypes.Contains(ItemType.BowgunAmmo))
                table.Add(ItemType.BowgunAmmo, ammo * 0.1);
            if (ammoTypes.Contains(ItemType.ExplosiveRounds) ||
                ammoTypes.Contains(ItemType.FlameRounds) ||
                ammoTypes.Contains(ItemType.AcidRounds))
            {
                table.Add(ItemType.ExplosiveRounds, ammo * 0.1);
                table.Add(ItemType.AcidRounds, ammo * 0.1);
                table.Add(ItemType.FlameRounds, ammo * 0.1);
            }
            if (ammoTypes.Contains(ItemType.SparkshotAmmo))
                table.Add(ItemType.SparkshotAmmo, ammo * 0.1);
            if (ammoTypes.Contains(ItemType.SMGAmmo))
                table.Add(ItemType.SMGAmmo, ammo * 0.1);

            bool successful;
            do
            {
                var itemType = table.Next();
                successful = SpawnItem(shuffled, itemType, GetRandomAmount(itemType));
            } while (successful);
        }

        private bool SpawnItem(Queue<ItemPoolEntry> pool, ItemType itemType, byte amount)
        {
            if (pool.Count != 0)
            {
                var oldEntry = pool.Dequeue();
                var newEntry = oldEntry;
                newEntry.Type = (byte)itemType;
                newEntry.Amount = amount;
                _logger.WriteLine($"    Replaced {oldEntry} with {newEntry}");
                _definedPool.Add(newEntry);
                return true;
            }
            else
            {
                return false;
            }
        }

        private ItemType GetAmmoTypeForWeapon(ItemType type)
        {
            switch (type)
            {
                case ItemType.HandgunLeon:
                case ItemType.HandgunClaire:
                case ItemType.CustomHandgun:
                case ItemType.ColtSAA:
                case ItemType.Beretta:
                    return ItemType.HandgunAmmo;
                case ItemType.Shotgun:
                    return ItemType.ShotgunAmmo;
                case ItemType.Magnum:
                case ItemType.CustomMagnum:
                    return ItemType.MagnumAmmo;
                case ItemType.Bowgun:
                    return ItemType.BowgunAmmo;
                case ItemType.SparkshotAmmo:
                    return ItemType.SparkshotAmmo;
                case ItemType.Flamethrower:
                    return ItemType.FuelTank;
                case ItemType.SMG:
                    return ItemType.SMGAmmo;
                case ItemType.GrenadeLauncherFlame:
                    return ItemType.GrenadeLauncherFlame;
                case ItemType.GrenadeLauncherExplosive:
                    return ItemType.GrenadeLauncherExplosive;
                case ItemType.GrenadeLauncherAcid:
                    return ItemType.GrenadeLauncherAcid;
                default:
                    return type;
            }
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
                case ItemType.ExplosiveRounds:
                case ItemType.AcidRounds:
                case ItemType.FlameRounds:
                    return (byte)_rng.Next(1, (int)(10 * multiplier));
                case ItemType.SparkshotAmmo:
                case ItemType.SMGAmmo:
                    return (byte)_rng.Next(1, (int)(100 * multiplier));
                case ItemType.RocketLauncher:
                    return (byte)_rng.Next(1, (int)(5 * multiplier));
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
            _nodeMap.Add(rdtId, node);

            var mapRoom = _map.GetRoom(rdtId);
            if (mapRoom != null)
            {
                node.Requires = mapRoom.Requires ?? Array.Empty<ushort>();

                if (mapRoom.Doors != null)
                {
                    foreach (var door in mapRoom.Doors)
                    {
                        if (door.Scenario != null && door.Scenario != _config.Scenario)
                            continue;

                        var edgeNode = GetOrCreateNode(RdtId.Parse(door.Target!));
                        var edge = new PlayEdge(edgeNode, door.Locked, door.NoReturn, door.Requires!);
                        node.Edges.Add(edge);
                    }
                }

                if (mapRoom.Items != null)
                {
                    foreach (var correctedItem in mapRoom.Items)
                    {
                        if (correctedItem.Scenario != null && correctedItem.Scenario != _config.Scenario)
                            continue;

                        var idx = Array.FindIndex(items, x => x.Id == correctedItem.Id);
                        if (idx != -1)
                        {
                            if (correctedItem.Link == null)
                            {
                                items[idx].Type = (ushort)correctedItem.Type;
                                items[idx].Amount = correctedItem.Amount ?? items[idx].Amount;
                            }
                            else
                            {
                                items[idx].Type = 0;

                                var rdtItemId = RdtItemId.Parse(correctedItem.Link);
                                node.LinkedItems.Add(correctedItem.Id, rdtItemId);
                            }
                            items[idx].Requires = correctedItem.Requires;
                            if (correctedItem.Priority != null)
                            {
                                items[idx].Priority = (ItemPriority)Enum.Parse(typeof(ItemPriority), correctedItem.Priority, true);
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
            if (_nodeMap.TryGetValue(rdtId, out var node))
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
