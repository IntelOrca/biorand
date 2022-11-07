using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard
{
    internal class ItemRandomiser
    {
        private readonly RandoLogger _logger;
        private RandoConfig _config;
        private GameData _gameData;
        private PlayNode[] _nodes = new PlayNode[0];
        private List<ItemPoolEntry> _futurePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _currentPool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _shufflePool = new List<ItemPoolEntry>();
        private List<ItemPoolEntry> _definedPool = new List<ItemPoolEntry>();
        private List<(RdtItemId, RdtItemId)> _linkedItems = new List<(RdtItemId, RdtItemId)>();
        private HashSet<ushort> _requiredItems = new HashSet<ushort>();
        private HashSet<ushort> _haveItems = new HashSet<ushort>();
        private HashSet<PlayNode> _visitedRooms = new HashSet<PlayNode>();
        private HashSet<RdtItemId> _visitedItems = new HashSet<RdtItemId>();
        private Rng _rng;
        private bool _searchForOriginalKeyLocation;
        private bool _debugLogging = false;

        public ItemRandomiser(RandoLogger logger, RandoConfig config, GameData gameData, Map map, Rng random)
        {
            _logger = logger;
            _config = config;
            _gameData = gameData;
            _rng = random;
        }

        public void RandomiseItems(PlayGraph graph)
        {
            _nodes = graph.GetAllNodes();

            _logger.WriteHeading("Randomizing Items:");
            _logger.WriteLine("Placing key items:");

            // Leon starts with a lighter
            if (_config.Player == 0)
            {
                _haveItems.Add((ushort)ItemType.Lighter);
            }

            var checkpoint = graph.Start;
            _visitedRooms.Clear();
            while (!_visitedRooms.Contains(graph.End) || _requiredItems.Count != 0)
            {
                PlaceKeyItem(_config.RandomDoors || _config.AlternativeRoutes);
                var newCheckpoint = Search(checkpoint);
                if (newCheckpoint != checkpoint && _requiredItems.Count == 0)
                {
                    _logger.WriteLine("    ------------ checkpoint ------------");
                    if (_config.RandomDoors || _config.ProtectFromSoftLock)
                    {
                        _shufflePool.AddRange(_currentPool.Where(x => x.Priority == ItemPriority.Normal));
                        _currentPool.Clear();
                    }
                    if (_config.RandomDoors)
                    {
                        _haveItems.Clear();
                        foreach (var item in newCheckpoint.DoorRandoAllRequiredItems)
                        {
                            _haveItems.Add(item);
                        }
                    }
                    checkpoint = newCheckpoint;
                }
            }
            _shufflePool.AddRange(_currentPool.Where(x => x.Priority == ItemPriority.Normal));
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
                        if (_visitedItems.Add(item.RdtItemId))
                            _currentPool.Add(item);

                        if (_currentPool.DistinctBy(x => x.RdtItemId).Count() != _currentPool.Count())
                            throw new Exception();

                        _futurePool.RemoveAll(x => x.RdtItemId == item.RdtItemId);
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
                    if (edge.Node == null)
                        continue;
                    if (edge.Lock != LockKind.None && edge.Lock != LockKind.Unblock)
                        continue;
                    if (!edge.RequiresRoom.All(x => _visitedRooms.Contains(x)))
                        continue;

                    var requiredItems = edge.Requires == null ? new ushort[0] : edge.Requires.Except(_haveItems).ToArray()!;
                    if (requiredItems.Length == 0)
                    {
                        if (seen.Contains(edge.Node.RdtId))
                            continue;

                        if (edge.NoReturn)
                        {
                            if (!_visitedRooms.Contains(edge.Node))
                            {
                                checkpoint = edge.Node;
                                if (_debugLogging)
                                {
                                    _logger.WriteLine($"        {node} -> {edge.Node} (checkpoint)");
                                }
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

        private void PlaceKeyItem(bool alternativeRoutes)
        {
            if (_requiredItems.Count == 0)
                return;

            var checkList = _requiredItems.Shuffle(_rng);
            foreach (var req in checkList)
            {
                if (PlaceKeyItem(req, alternativeRoutes))
                {
                    if (IsTwinItem(req))
                    {
                        if (!PlaceKeyItem(req, true))
                        {
                            throw new Exception($"Unable to place 2nd {(ItemType)req}");
                        }
                    }
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
                _logger.WriteLine($"        {Items.GetItemName(item)}");
            }

            if (_requiredItems.Any(x => !IsOptionalItem(x)))
                throw new Exception("Unable to find key item to swap");

            _requiredItems.Clear();
        }

        private static bool IsOptionalItem(ushort item)
        {
            switch ((ItemType)item)
            {
                case ItemType.FilmA:
                case ItemType.FilmB:
                case ItemType.FilmC:
                case ItemType.FilmD:
                case ItemType.Cord:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTwinItem(ushort item)
        {
            return item == (ushort)ItemType.SmallKey || item == (ushort)ItemType.RedJewel;
        }

        private bool PlaceKeyItem(ushort req, bool alternativeRoute)
        {
            // Get a new location for the key item
            var index = FindNewKeyItemLocation(req);
            if (index == null)
                throw new Exception("Run out of free item slots");

            var itemEntry = _currentPool[index.Value];
            var itemParentNode = _nodes.First(x => x.RdtId == itemEntry.RdtId);

            if (!_config.RandomDoors)
            {
                // Find original location of key item
                var originalIndex = _currentPool.FindIndex(x => x.Type == req);
                if (originalIndex == -1 && alternativeRoute)
                {
                    // Key must be in a later area, find it
                    foreach (var node in _nodes)
                    {
                        foreach (var item in node.Items)
                        {
                            if (item.Type == req && item.Priority != ItemPriority.Fixed && !_visitedItems.Contains((RdtItemId)item.RdtItemId))
                            {
                                _futurePool.Add(item);
                                _currentPool.Add(item);
                                if (_currentPool.DistinctBy(x => x.RdtItemId).Count() != _currentPool.Count())
                                    throw new Exception();
                                _visitedItems.Add(item.RdtItemId);
                                originalIndex = _currentPool.Count - 1;
                                break;
                            }
                        }
                    }
                }
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
            else
            {
                itemEntry.Amount = GetTotalKeyRequirementCount(itemParentNode, req);
            }
            itemEntry.Type = req;

            // Remove new key item location from pool
            _requiredItems.Remove(req);
            _haveItems.Add(req);
            _currentPool.RemoveAt(index.Value);
            _definedPool.Add(itemEntry);
            itemParentNode.PlacedKeyItems.Add(itemEntry);

            _logger.WriteLine($"    Placing key item ({Items.GetItemName(itemEntry.Type)}) in {itemEntry.RdtId}:{itemEntry.Id}");
            return true;
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
            var ammoTypes = new HashSet<ItemType>() { ItemType.HandgunAmmo };
            var items = new List<ItemType>();
            if (_config.Player == 0)
            {
                if (_rng.Next(0, 3) >= 1)
                    items.Add(ItemType.HandgunParts);
                if (_rng.Next(0, 2) >= 1)
                {
                    items.Add(ItemType.Shotgun);
                    if (_rng.Next(0, 2) >= 1)
                        items.Add(ItemType.ShotgunParts);
                }
                if (_rng.Next(0, 3) >= 1)
                {
                    items.Add(ItemType.Magnum);
                    if (_rng.Next(0, 2) >= 1)
                        items.Add(ItemType.MagnumParts);
                }
                if (_rng.Next(0, 2) == 0)
                    items.Add(ItemType.SMG);
                if (_rng.Next(0, 2) == 0)
                    items.Add(ItemType.Flamethrower);
            }
            else
            {
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
            }
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
            if (ammoTypes.Contains(ItemType.ShotgunAmmo))
                table.Add(ItemType.ShotgunAmmo, ammo * 0.2);
            if (ammoTypes.Contains(ItemType.BowgunAmmo))
                table.Add(ItemType.BowgunAmmo, ammo * 0.1);
            if (ammoTypes.Contains(ItemType.MagnumAmmo))
                table.Add(ItemType.MagnumAmmo, ammo * 0.1);
            if (ammoTypes.Contains(ItemType.ExplosiveRounds) ||
                ammoTypes.Contains(ItemType.FlameRounds) ||
                ammoTypes.Contains(ItemType.AcidRounds))
            {
                table.Add(ItemType.ExplosiveRounds, ammo * 0.1);
                table.Add(ItemType.AcidRounds, ammo * 0.1);
                table.Add(ItemType.FlameRounds, ammo * 0.1);
            }
            if (ammoTypes.Contains(ItemType.FuelTank))
                table.Add(ItemType.FuelTank, ammo * 0.1);
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
                case ItemType.ShotgunAmmo:
                    return (byte)_rng.Next(1, (int)(30 * multiplier));
                case ItemType.BowgunAmmo:
                    return (byte)_rng.Next(1, (int)(30 * multiplier));
                case ItemType.MagnumAmmo:
                    return (byte)_rng.Next(1, (int)(10 * multiplier));
                case ItemType.ExplosiveRounds:
                case ItemType.AcidRounds:
                case ItemType.FlameRounds:
                    return (byte)_rng.Next(1, (int)(10 * multiplier));
                case ItemType.FuelTank:
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

        private ushort GetTotalKeyRequirementCount(PlayNode node, ushort keyType)
        {
            if (!IsItemTypeDiscardable((ItemType)keyType))
            {
                return 1;
            }

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

        private static bool IsItemTypeDiscardable(ItemType itemType)
        {
            switch (itemType)
            {
                case ItemType.CabinKey:
                case ItemType.SpadeKey:
                case ItemType.DiamondKey:
                case ItemType.HeartKey:
                case ItemType.ClubKey:
                case ItemType.PowerRoomKey:
                case ItemType.UmbrellaKeyCard:
                case ItemType.MasterKey:
                case ItemType.PlatformKey:
                    return true;
                default:
                    return false;
            }
        }
    }
}
