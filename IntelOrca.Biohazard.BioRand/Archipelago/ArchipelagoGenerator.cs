using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand.Archipelago
{
    internal class ArchipelagoGenerator
    {
        private readonly IDoorHelper _doorHelper;
        private readonly IItemHelper _itemHelper;
        private readonly List<ArchipelagoRegion> _regions = new List<ArchipelagoRegion>();
        private readonly List<ArchipelagoLocation> _locations = new List<ArchipelagoLocation>();
        private readonly HashSet<int> _locationIds = new HashSet<int>();
        private readonly List<ArchipelagoItem> _items = new List<ArchipelagoItem>();
        private readonly HashSet<PlayNode> _visited = new HashSet<PlayNode>();
        private readonly Dictionary<PlayNode, ArchipelagoRegion> _nodeToRegionMap = new Dictionary<PlayNode, ArchipelagoRegion>();

        public ArchipelagoGenerator(IDoorHelper doorHelper, IItemHelper itemHelper)
        {
            _doorHelper = doorHelper;
            _itemHelper = itemHelper;
        }

        public void Generate(string path, PlayGraph graph, string? seed = null, string? description = null)
        {
            var exitNodes = new Queue<PlayEdge>();
            GetNextRegion(graph.Start!, exitNodes);
            while (!_visited.Contains(graph.End!) && exitNodes.Count != 0)
            {
                var edge = exitNodes.Dequeue();
                if (_visited.Contains(edge.Node!))
                    continue;

                var regionId = GetNextRegion(edge.Node!, exitNodes);
                var fromRegion = _nodeToRegionMap[edge.Parent];
                AddEdgeToRegion(fromRegion, edge);
            }

            AddVictoryEvent(graph.End!);

            // Convert require arrays to AP item ids
            foreach (var region in _regions)
            {
                if (region.Edges != null)
                {
                    foreach (var edge in region.Edges)
                    {
                        edge.Requires = ConvertRequiresArray(edge.Requires);
                    }
                }
            }
            foreach (var location in _locations)
            {
                location.Requires = ConvertRequiresArray(location.Requires);
            }

            var output = new ArchipelagoData()
            {
                Description = description,
                Seed = seed,
                Regions = _regions.ToArray(),
                Locations = _locations.ToArray(),
                Items = _items
                    .DistinctBy(x => x.Id)
                    .OrderBy(x => x.Type)
                    .ThenBy(x => x.Amount)
                    .ToArray()
            };
            var outputJson = JsonSerializer.Serialize(output, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                WriteIndented = true
            });
            File.WriteAllText(path, outputJson);
        }

        private void AddVictoryEvent(PlayNode victoryNode)
        {
            // Add fake victory location with victory item
            var victoryItemId = 0x10000;
            _items.Add(new ArchipelagoItem()
            {
                Id = victoryItemId,
                Name = "Victory",
                Group = "event"
            });
            var victoryLocation = new ArchipelagoLocation()
            {
                Id = _locations.Count,
                Name = "Victory",
                Item = victoryItemId
            };
            _locations.Add(victoryLocation);
            var endRegion = _nodeToRegionMap[victoryNode];
            endRegion.Locations = endRegion.Locations
                .Concat(new[] { victoryLocation.Id!.Value })
                .ToArray();
        }

        private int[]? ConvertRequiresArray(int[]? input)
        {
            if (input == null)
                return null;

            var output = input
                .Select(type => FindItemId(type))
                .Where(x => x != null)
                .Select(x => x!.Value)
                .ToArray();
            return output.Length == 0 ? null : output;
        }

        private int? FindItemId(int type)
        {
            foreach (var item in _items)
            {
                if (item.Type == type)
                    return item.Id;
            }
            return null;
        }

        private int GetNextRegion(PlayNode startNode, Queue<PlayEdge> exits)
        {
            var regionId = _regions.Count;
            var region = new ArchipelagoRegion()
            {
                Id = regionId,
                Name = $"Region {regionId}"
            };
            _regions.Add(region);

            var regionEdges = new List<ArchipelagoEdge>();
            var regionNodes = new List<PlayNode>();
            var locations = new List<ArchipelagoLocation>();

            var queue = new Queue<PlayNode>();
            queue.Enqueue(startNode);
            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                if (!_visited.Add(node))
                    continue;

                _nodeToRegionMap.Add(node, region);
                regionNodes.Add(node);
                ProcessRoomItems(node, locations);

                foreach (var edge in node.Edges)
                {
                    if (IsLockedEdge(edge))
                        continue;

                    var edgeNode = edge.Node!;
                    if (_nodeToRegionMap.TryGetValue(edgeNode, out var exitRegion))
                    {
                        AddEdgeToRegion(region, edge);
                    }
                    else if (IsExitEdge(edge))
                    {
                        exits.Enqueue(edge);
                    }
                    else
                    {
                        queue.Enqueue(edgeNode);
                    }
                }
            }
            region.Locations = locations.Select(x => x.Id!.Value).ToArray();
            return regionId;
        }

        private void AddEdgeToRegion(ArchipelagoRegion region, PlayEdge edge)
        {
            var edgeRegion = _nodeToRegionMap[edge.Node!];
            if (edgeRegion == region)
                return;

            foreach (var oldEdge in region.Edges!)
            {
                if (oldEdge.Region == edgeRegion.Id)
                    return;
            }

            var apEdge = new ArchipelagoEdge();
            apEdge.Region = edgeRegion.Id;
            apEdge.Requires = edge.Requires.Select(x => (int)x).ToArray();
            region.Edges!.Add(apEdge);
        }

        private static bool IsExitEdge(PlayEdge edge)
        {
            if (edge.Requires?.Length != 0)
                return true;
            return false;
        }

        private static bool IsLockedEdge(PlayEdge edge)
        {
            if (edge.Node == null)
                return true;
            if (edge.Lock == LockKind.Always || edge.Lock == LockKind.Gate || edge.Lock == LockKind.Side)
                return true;
            return false;
        }

        private ArchipelagoLocation[] ProcessRoomItems(PlayNode node, List<ArchipelagoLocation> locations)
        {
            var roomDisplayName = _doorHelper.GetRoomDisplayName(node.RdtId);
            foreach (var item in node.PlacedKeyItems.Concat(node.PlacedNonKeyItems))
            {
                var itemAttributes = _itemHelper.GetItemAttributes((byte)item.Type);
                var hasQuantity =
                    itemAttributes.HasFlag(ItemAttribute.Ammo) ||
                    itemAttributes.HasFlag(ItemAttribute.InkRibbon);
                var itemName = _itemHelper.GetItemName((byte)item.Type);
                var itemDisplayName = hasQuantity ?
                    $"{itemName} (x{item.Amount})" : itemName;

                var apItem = new ArchipelagoItem()
                {
                    Type = item.Type,
                    Amount = item.Amount,
                    Name = itemDisplayName,
                    Group = GetItemGroup(itemAttributes)
                };

                _items.Add(apItem);

                // Can't add another item which shares same global ID, give it a non-clash ID
                var id = (int)item.GlobalId;
                var priority = item.Priority;
                if (!_locationIds.Add(id))
                {
                    id = 0x20000 | _locations.Count;
                    priority = ItemPriority.Low;
                }

                var location = new ArchipelagoLocation()
                {
                    Id = id,
                    Name = $"{item.RdtItemId} {roomDisplayName}",
                    Item = apItem.Id!.Value,
                };
                if (item.Priority != ItemPriority.Normal)
                    location.Priority = item.Priority.ToString().ToLowerInvariant();
                if (item.Requires != null && item.Requires.Length != 0)
                    location.Requires = item.Requires.Select(x => (int)x).ToArray();
                locations.Add(location);
                _locations.Add(location);
            }
            return locations.ToArray();
        }

        private static string? GetItemGroup(ItemAttribute attributes)
        {
            if (attributes.HasFlag(ItemAttribute.Weapon))
                return "weapon";
            if (attributes.HasFlag(ItemAttribute.Ammo))
                return "ammo";
            if (attributes.HasFlag(ItemAttribute.Gunpowder))
                return "gunpowder";
            if (attributes.HasFlag(ItemAttribute.Heal))
                return "heal";
            if (attributes.HasFlag(ItemAttribute.InkRibbon))
                return "ink";
            if (attributes.HasFlag(ItemAttribute.Key))
                return "key";
            if (attributes.HasFlag(ItemAttribute.Document))
                return "document";
            return null;
        }
    }
}
