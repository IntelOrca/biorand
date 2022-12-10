using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard
{
    internal class ItemRandomiser
    {
        private static bool g_debugLogging = false;

        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private Rng _rng;
        private IItemHelper _itemHelper;

        private PlayNode[] _nodes = new PlayNode[0];
        private List<ItemPoolEntry> _currentPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _shufflePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private List<(RdtItemId, RdtItemId)> _linkedItems = new List<(RdtItemId, RdtItemId)>();
        private HashSet<KeyRequirement> _requiredItems = new HashSet<KeyRequirement>();
        private HashSet<ushort> _haveItems = new HashSet<ushort>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private HashSet<RdtItemId> _visitedItems = new HashSet<RdtItemId>();

        public ItemRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Rng random, IItemHelper itemHelper)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _rng = random;
            _itemHelper = itemHelper;
        }

        public void RandomiseItems(PlayGraph graph)
        {
            if (graph.Start == null || graph.End == null)
                throw new ArgumentException("No start or end node", nameof(graph));

            _nodes = graph.GetAllNodes();

            _logger.WriteHeading("Randomizing Items:");
            _logger.WriteLine("Placing key items:");

            var checkpoint = graph.Start;
            ClearItems();
            _visitedRooms.Clear();
            while (!_visitedRooms.Contains(graph.End) || !JustOptionalItemsLeft())
            {
                PlaceKeyItem(_config.RandomDoors || _config.AlternativeRoutes);
                var newCheckpoint = Search(checkpoint);
                if (newCheckpoint != checkpoint && _requiredItems.Count == 0)
                {
                    _logger.WriteLine("    ------------ checkpoint ------------");
                    if (_config.RandomDoors || _config.ProtectFromSoftLock)
                    {
                        _shufflePool.AddRange(_currentPool.Where(x => x.Priority != ItemPriority.Fixed));
                        _currentPool.Clear();
                    }
                    if (_config.RandomDoors)
                    {
                        ClearItems();
                        foreach (var item in newCheckpoint.DoorRandoAllRequiredItems)
                        {
                            _haveItems.Add(item);
                        }
                    }
                    checkpoint = newCheckpoint;
                }
            }
            _shufflePool.AddRange(_currentPool.Where(x => x.Priority != ItemPriority.Fixed));
            if (_shufflePool.DistinctBy(x => x.RdtItemId).Count() != _shufflePool.Count())
                throw new Exception();

            if (!_config.RandomDoors && _config.ShuffleItems)
            {
                ShuffleRemainingPool();
            }
            else
            {
                RandomiseRemainingPool();
            }

            SetLinkedItems();
            SetItems();
            PatchDesk();
        }

        private bool JustOptionalItemsLeft()
        {
            if (_requiredItems.Count == 0)
                return true;

            return _requiredItems.All(x => x.Item == null || _itemHelper.IsOptionalItem(_config, (byte)x.Item.Value.Type));
        }

        private void ClearItems()
        {
            // Leon starts with a lighter
            _haveItems.Clear();
            foreach (var item in _itemHelper.GetInitialItems(_config))
            {
                _haveItems.Add(item);
            }
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
                    if (node.Requires.Length != 0)
                        _requiredItems.Add(new KeyRequirement(node.Requires));

                    // First time we have visited room, add room items to pool
                    foreach (var item in node.Items)
                    {
                        // Don't add items we require a key for
                        if (item.Requires != null && item.Requires.Length != 0)
                        {
                            _requiredItems.Add(new KeyRequirement(item.Requires, item));
                        }
                        else
                        {
                            AddItemToPool(item);
                        }
                    }
                    foreach (var linkedItem in node.LinkedItems)
                    {
                        _linkedItems.Add((new RdtItemId(node.RdtId, linkedItem.Key), linkedItem.Value));
                    }

                    if (g_debugLogging && node.Items.Length != 0)
                    {
                        _logger.WriteLine($"    Room {node.RdtId} contains:");
                        foreach (var item in node.Items)
                        {
                            _logger.WriteLine($"        {item}");
                        }
                    }
                }

                foreach (var edge in node.Edges)
                {
                    if (edge.Node == null)
                        continue;
                    if (edge.Lock != LockKind.None && edge.Lock != LockKind.Unblock)
                        continue;
                    if (!edge.RequiresRoom.All(x => _visitedRooms.Contains(x)))
                        continue;

                    var requiredItems = edge.Requires == null ? new ushort[0] : edge.Requires.Except(_haveItems).ToArray()!;
                    var justOptionalLeft = requiredItems.All(x => _itemHelper.IsOptionalItem(_config, (byte)x));
                    if (requiredItems.Length == 0 || justOptionalLeft)
                    {
                        if (seen.Contains(edge.Node.RdtId))
                            continue;

                        if (edge.NoReturn)
                        {
                            if (!_visitedRooms.Contains(edge.Node))
                            {
                                checkpoint = edge.Node;
                                if (g_debugLogging)
                                {
                                    _logger.WriteLine($"        {node} -> {edge.Node} (checkpoint)");
                                }
                            }
                        }
                        else
                        {
                            if (g_debugLogging)
                            {
                                _logger.WriteLine($"        {node} -> {edge.Node}");
                            }
                            stack.Push(edge.Node);
                        }
                    }
                    else
                    {
                        _requiredItems.Add(new KeyRequirement(requiredItems, null, isDoor: true));
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

        private int? FindNewKeyItemLocation(int type, bool includeLowPriority)
        {
            var randomOrder = Enumerable.Range(0, _currentPool.Count).Shuffle(_rng).ToArray();
            var bestI = (int?)null;
            foreach (var i in randomOrder)
            {
                var item = _currentPool[i];
                if (item.Priority == ItemPriority.Fixed)
                    continue;

                if (!includeLowPriority && item.Priority == ItemPriority.Low)
                    continue;

                if (!HasAllRequiredItems(item.Requires))
                    continue;

                if (item.Type == type)
                    bestI = i;
                else
                    return i;
            }
            return bestI;
        }

        private static string GetNth(int i)
        {
            while (i >= 20)
                i /= 10;
            if (i == 1)
                return $"{i}st";
            if (i == 2)
                return $"{i}nd";
            if (i == 3)
                return $"{i}rd";
            return $"{i}th";
        }

        private void PlaceKeyItem(bool alternativeRoutes)
        {
            var keyItemPlaceOrder = GetKeyItemPlaceOrder();
            if (keyItemPlaceOrder.Length == 0)
                return;

            var checkList = GetKeyItemPlaceOrder();
            foreach (var req in checkList)
            {
                if (PlaceKeyItem(req, alternativeRoutes))
                {
                    var quantity = _itemHelper.GetItemQuantity(_config, (byte)req);
                    for (int i = 1; i < quantity; i++)
                    {
                        if (!PlaceKeyItem(req, true))
                        {
                            throw new Exception($"Unable to place {GetNth(i + 1)} {_itemHelper.GetItemName((byte)req)}");
                        }
                    }
                    UpdateRequiredItemList();
                    return;
                }
            }

            if (!alternativeRoutes)
            {
                // Failed, so try with alternative routes
                // This is a hack to get round the blue card
                PlaceKeyItem(true);
                return;
            }

            _logger.WriteLine("    Unable to place the following key items:");
            foreach (var item in checkList)
            {
                _logger.WriteLine($"        {_itemHelper.GetItemName((byte)item)}");
            }

            if (keyItemPlaceOrder.Any(x => !_itemHelper.IsOptionalItem(_config, (byte)x)))
                throw new Exception("Unable to find key item to swap");

            _requiredItems.Clear();
        }

        private ushort[] GetKeyItemPlaceOrder()
        {
            UpdateRequiredItemList();
            return _requiredItems
                .Shuffle(_rng)
                .OrderBy(x => x.IsDoor ? 0 : 1)
                .ThenBy(x => x.Keys.Length)
                .SelectMany(x => x.Keys)
                .Where(x => !_haveItems.Contains(x))
                .ToArray();
        }

        private void UpdateRequiredItemList()
        {
            var newItemsForPool = _requiredItems
                .Where(x => x.Item != null && x.Keys.All(x => _haveItems.Contains(x)))
                .Select(x => x.Item)
                .ToArray();
            foreach (var rq in newItemsForPool)
            {
                AddItemToPool(rq!.Value);
            }

            _requiredItems.RemoveWhere(x => x.Keys.All(x => _haveItems.Contains(x)));
        }

        private bool PlaceKeyItem(ushort req, bool alternativeRoute)
        {
            // Get a new location for the key item
            var index = FindNewKeyItemLocation(req, false);
            if (index == null)
            {
                index = FindNewKeyItemLocation(req, true);
                if (index == null)
                {
                    throw new Exception("Run out of free item slots");
                }
            }

            var itemEntry = _currentPool[index.Value];
            var itemParentNode = _nodes.First(x => x.RdtId == itemEntry.RdtId);

            if (!_config.RandomDoors)
            {
                // Find original location of key item
                var futureItem = false;
                var originalIndex = _currentPool.FindIndex(x => x.Type == req);
                if (originalIndex == -1 && alternativeRoute)
                {
                    var t = FindKeyInLaterArea(req);
                    if (t != null)
                    {
                        var (futureNode, itemIndex) = t.Value;
                        if (g_debugLogging)
                        {
                            _logger.WriteLine($"        Found key as future item ({futureNode.Items[itemIndex]})");
                        }

                        // Change room item to the item we are going to replace
                        var kc = futureNode.Items[itemIndex].Amount;
                        futureNode.Items[itemIndex].Type = itemEntry.Type;
                        futureNode.Items[itemIndex].Amount = itemEntry.Amount;
                        itemEntry.Amount = kc;
                        futureItem = true;
                    }
                }
                if (!futureItem)
                {
                    if (originalIndex == -1)
                    {
                        // Check original key was in a previous checkpoint area (edge case for Weapon Box Key)
                        var shuffleIndex = _shufflePool.FindIndex(x => x.Type == req);
                        if (shuffleIndex != -1)
                        {
                            var item = _shufflePool[shuffleIndex];
                            _shufflePool.RemoveAt(shuffleIndex);
                            _currentPool.Add(item);
                            originalIndex = _currentPool.Count - 1;
                        }
                    }
                    if (originalIndex == -1)
                    {
                        return false;
                    }

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
            }
            else
            {
                itemEntry.Amount = GetTotalKeyRequirementCount(itemParentNode, req);
            }
            itemEntry.Type = req;

            // Remove new key item location from pool
            _haveItems.Add(req);
            _currentPool.RemoveAt(index.Value);
            _definedPool.Add(itemEntry);
            itemParentNode.PlacedKeyItems.Add(itemEntry);

            _logger.WriteLine($"    Placing key item ({_itemHelper.GetItemName((byte)itemEntry.Type)} x{itemEntry.Amount}) in {itemEntry.RdtId}:{itemEntry.Id}");
            return true;
        }

        private (PlayNode, int)? FindKeyInLaterArea(ushort req)
        {
            foreach (var node in _nodes)
            {
                for (int i = 0; i < node.Items.Length; i++)
                {
                    var item = node.Items[i];
                    if (item.Type == req && item.Priority != ItemPriority.Fixed && !_visitedItems.Contains(item.RdtItemId))
                    {
                        return (node, i);
                    }
                }
            }
            return null;
        }

        private int AddItemToPool(ItemPoolEntry item)
        {
            if (_visitedItems.Add(item.RdtItemId))
            {
                _currentPool.Add(item);
                if (g_debugLogging)
                    _logger.WriteLine($"    Add {item} to current pool");
            }

            if (_currentPool.DistinctBy(x => x.RdtItemId).Count() != _currentPool.Count())
                throw new Exception();

            return _currentPool.Count - 1;
        }

        private void RandomiseRemainingPool()
        {
            _logger.WriteLine("Randomizing non-key items:");
            if (_config.RatioAmmo == 0 && _config.RatioHealth == 0 && _config.RatioInkRibbons == 0)
            {
                throw new Exception("No item ratios have been set.");
            }

            // Shuffle the pool, keep low priority at the end
            var shuffled = _shufflePool
                .Where(x => x.Priority == ItemPriority.Normal)
                .Shuffle(_rng)
                .Concat(_shufflePool.Where(x => x.Priority == ItemPriority.Low))
                .ToQueue();
            _shufflePool.Clear();

            // Weapons first
            var ammoTypes = new HashSet<byte>() { _itemHelper.GetItemId(CommonItemKind.HandgunAmmo) };
            var items = _itemHelper.GetWeapons(_rng, _config);
            foreach (var itemType in items)
            {
                // Spawn weapon
                var amount = GetRandomAmount(itemType, true);
                SpawnItem(shuffled, itemType, amount);

                // Add supported ammo types
                var weaponAmmoTypes = _itemHelper.GetAmmoTypeForWeapon(itemType);
                foreach (var ammoType in weaponAmmoTypes)
                {
                    ammoTypes.Add(ammoType);
                }
            }

            // Now everything else
            var ammoTable = _rng.CreateProbabilityTable<byte>();
            foreach (var ammoType in ammoTypes)
            {
                var probability = _itemHelper.GetItemProbability(ammoType);
                ammoTable.Add(ammoType, probability);
            }

            var healthTable = _rng.CreateProbabilityTable<byte>();
            healthTable.Add(_itemHelper.GetItemId(CommonItemKind.GreenHerb), 0.5);
            healthTable.Add(_itemHelper.GetItemId(CommonItemKind.RedHerb), 0.3);
            healthTable.Add(_itemHelper.GetItemId(CommonItemKind.BlueHerb), 0.1);
            healthTable.Add(_itemHelper.GetItemId(CommonItemKind.FirstAid), 0.1);

            var inkTable = _rng.CreateProbabilityTable<byte>();
            inkTable.Add(_itemHelper.GetItemId(CommonItemKind.InkRibbon), 1);

            var numAmmo = (int)((_config.RatioAmmo / 32.0) * shuffled.Count);
            var numHealth = (int)((_config.RatioHealth / 32.0) * shuffled.Count);
            var numInk = (int)((_config.RatioInkRibbons / 32.0) * shuffled.Count);

            var proportions = new List<(int, Rng.Table<byte>)>();
            proportions.Add((numAmmo, ammoTable));
            proportions.Add((numHealth, healthTable));
            if (_itemHelper.HasInkRibbons(_config))
            {
                proportions.Add((numInk, inkTable));
            }
            proportions = proportions
                .Where(x => x.Item1 != 0)
                .OrderBy(x => x.Item1)
                .ToList();
            var lastP = proportions[proportions.Count - 1];
            lastP.Item1 = int.MaxValue;
            proportions[proportions.Count - 1] = lastP;

            foreach (var p in proportions)
            {
                SpawnItems(shuffled, p.Item1, p.Item2);
            }
        }

        private void SpawnItems(Queue<ItemPoolEntry> pool, int count, Rng.Table<byte> probabilityTable)
        {
            for (int i = 0; i < count; i++)
            {
                var itemType = probabilityTable.Next();
                if (!SpawnItem(pool, itemType, GetRandomAmount(itemType, false)))
                {
                    break;
                }
            }
        }

        private bool SpawnItem(Queue<ItemPoolEntry> pool, byte itemType, byte amount)
        {
            if (pool.Count != 0)
            {
                var oldEntry = pool.Dequeue();
                var newEntry = oldEntry;
                newEntry.Type = itemType;
                newEntry.Amount = amount;
                _logger.WriteLine($"    Replaced {oldEntry.ToString(_itemHelper)} with {newEntry.ToString(_itemHelper)}");
                if (_definedPool.Any(x => x.RdtItemId == newEntry.RdtItemId))
                    throw new Exception();
                _definedPool.Add(newEntry);
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte GetRandomAmount(byte type, bool fullQuantity)
        {
            if (type == _itemHelper.GetItemId(CommonItemKind.InkRibbon))
            {
                return (byte)_rng.Next(1, 3);
            }

            var multiplier = fullQuantity ? 1 : (_config.AmmoQuantity / 8.0);
            var max = _itemHelper.GetMaxAmmoForAmmoType(type);
            return (byte)_rng.Next(1, (int)(max * multiplier) + 1);
        }

        private void ShuffleRemainingPool()
        {
            _logger.WriteLine("Shuffling non-key items:");
            var shufflePool = _shufflePool
                .Where(x => x.Priority != ItemPriority.Low)
                .ToArray();
            var shuffled = shufflePool.Shuffle(_rng);
            for (int i = 0; i < shufflePool.Length; i++)
            {
                var entry = shufflePool[i];
                entry.Type = shuffled[i].Type;
                entry.Amount = shuffled[i].Amount;
                _logger.WriteLine($"    Swapped {shufflePool[i]} with {shuffled[i]}");
                _definedPool.Add(entry);
            }
            _shufflePool.Clear();
        }

        private void SetLinkedItems()
        {
            _logger.WriteLine("Setting up linked items:");
            foreach (var (sourceId, targetId) in _linkedItems)
            {
                var sourceItemIndex = _definedPool.FindIndex(x => x.RdtItemId == sourceId);
                if (sourceItemIndex != -1)
                {
                    var sourceItem = _definedPool[sourceItemIndex];
                    var targetItem = sourceItem;
                    targetItem.RdtItemId = targetId;
                    _definedPool.Add(targetItem);
                    _logger.WriteLine($"    {sourceItem.ToString(_itemHelper)} placed at {targetId}");
                }
            }
        }

        private void SetItems()
        {
            foreach (var entry in _definedPool)
            {
                var rdt = _gameData.GetRdt(entry.RdtId)!;

                // HACK: 255 is used for give commands
                if (entry.Id == 255)
                {
                    foreach (var itemGet in rdt.ItemGets)
                    {
                        itemGet.Type = (byte)entry.Type;
                        itemGet.Amount = (byte)entry.Amount;
                    }
                }
                else
                {
                    rdt.SetItem(entry.Id, entry.Type, entry.Amount);
                }
            }
        }

        private void PatchDesk()
        {
            var rdt = _gameData.GetRdt(new RdtId(0, 0x15));
            if (rdt == null)
                return;

            // Only take 5 inspections for the item rather than 50
            if (_config.Player == 0)
                rdt.Patches.Add(new KeyValuePair<int, byte>(0x1A20, 5));
            else
                rdt.Patches.Add(new KeyValuePair<int, byte>(0x1C7A, 5));
        }

        private ushort GetTotalKeyRequirementCount(PlayNode node, ushort keyType)
        {
            if (_itemHelper.IsItemInfinite((byte)keyType))
                return 0;

            if (!_itemHelper.IsItemTypeDiscardable((byte)keyType))
                return 1;

            ushort total = 0;
            var visited = new HashSet<PlayNode>();
            var stack = new Stack<PlayNode>();
            stack.Push(node);
            visited.Add(node);
            while (stack.Count != 0)
            {
                var n = stack.Pop();
                if (n.Requires.Contains(keyType))
                    total++;

                foreach (var item in n.Items)
                {
                    if (item.Requires?.Contains(keyType) == true)
                        total++;
                }

                foreach (var edge in n.Edges)
                {
                    if (edge.Requires.Contains(keyType))
                        total++;
                    if (edge.Node != null && edge.Lock != LockKind.Always && visited.Add(edge.Node))
                    {
                        stack.Push(edge.Node);
                    }
                }
            }
            return total;
        }

        private class KeyRequirement : IEquatable<KeyRequirement>
        {
            public ushort[] Keys { get; }
            public ItemPoolEntry? Item { get; }
            public bool IsDoor { get; }

            public KeyRequirement(IEnumerable<ushort> keys)
                : this(keys, null, false)
            {
            }

            public KeyRequirement(IEnumerable<ushort> keys, ItemPoolEntry? item, bool isDoor = false)
            {
                Keys = keys.OrderBy(x => x).ToArray();
                if (Keys.Length == 0)
                    throw new ArgumentException(nameof(keys));
                Item = item;
                IsDoor = isDoor;
            }

            public override int GetHashCode()
            {
                return string.Join(",", Keys).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as KeyRequirement);
            }

            public bool Equals(KeyRequirement? other)
            {
                if (other == null)
                    return false;
                if (!Keys.SequenceEqual(other.Keys))
                    return false;
                if (!Item.Equals(other.Item))
                    return false;
                if (!Keys.SequenceEqual(other.Keys))
                    return false;
                return IsDoor == other.IsDoor;
            }

            public override string ToString()
            {
                return $"{string.Join(",", Keys.Select(x => Items.GetItemName(x)))}";
            }
        }
    }
}
