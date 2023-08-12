using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.BioRand.Archipelago
{
    internal class ArchipelagoGenerator
    {
        private readonly IItemHelper _itemHelper;
        private readonly List<ArchipelagoRegion> _regions = new List<ArchipelagoRegion>();
        private readonly List<ArchipelagoItem> _items = new List<ArchipelagoItem>();
        private readonly HashSet<PlayNode> _visited = new HashSet<PlayNode>();
        private readonly Dictionary<PlayNode, ArchipelagoRegion> _nodeToRegionMap = new Dictionary<PlayNode, ArchipelagoRegion>();

        public ArchipelagoGenerator(IItemHelper itemHelper)
        {
            _itemHelper = itemHelper;
        }

        public void Generate(string path, PlayGraph graph, string? seed = null, string? description = null)
        {
            var exitNodes = new Queue<(ArchipelagoRegion?, PlayNode)>();
            exitNodes.Enqueue((null, graph.Start!));

            while (!_visited.Contains(graph.End!) && exitNodes.Count != 0)
            {
                var (region, exitNode) = exitNodes.Dequeue();
                if (_visited.Contains(exitNode))
                    continue;

                var regionId = GetNextRegion(exitNode, exitNodes);
                if (region != null)
                    region.Edges!.Add(regionId);
            }

            if (_nodeToRegionMap.TryGetValue(graph.End!, out var victoryRegion))
            {
                victoryRegion.Victory = true;
            }

            var output = new ArchipelagoData()
            {
                Description = description,
                Seed = seed,
                Regions = _regions.ToArray(),
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

        private int GetNextRegion(PlayNode startNode, Queue<(ArchipelagoRegion?, PlayNode)> exitNodes)
        {
            var regionId = _regions.Count;
            var region = new ArchipelagoRegion()
            {
                Id = regionId,
                Name = $"Region {regionId}"
            };
            _regions.Add(region);
            _nodeToRegionMap.Add(startNode, region);

            var regionEdges = new List<ArchipelagoRegion>();
            var regionNodes = new List<PlayNode>();
            var locations = new List<ArchipelagoLocation>();

            var queue = new Queue<PlayNode>();
            queue.Enqueue(startNode);
            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                if (!_visited.Add(node))
                    continue;

                regionNodes.Add(node);
                ProcessRoomItems(node, locations);

                foreach (var edge in node.Edges)
                {
                    if (IsLockedEdge(edge))
                        continue;

                    var edgeNode = edge.Node!;
                    if (_nodeToRegionMap.TryGetValue(edgeNode, out var exitRegion))
                    {
                        if (exitRegion != region)
                            regionEdges.Add(exitRegion);
                    }
                    else if (IsExitEdge(edge))
                    {
                        exitNodes.Enqueue((region, edgeNode));
                    }
                    else
                    {
                        queue.Enqueue(edgeNode);
                    }
                }
            }
            region.Locations = locations.ToArray();
            region.Edges = regionEdges
                .Select(x => x.Id)
                .Distinct()
                .ToList();
            return regionId;
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
            if (edge.Lock == LockKind.Always)
                return true;
            return false;
        }

        private ArchipelagoLocation[] ProcessRoomItems(PlayNode node, List<ArchipelagoLocation> locations)
        {
            foreach (var item in node.Items)
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
                locations.Add(new ArchipelagoLocation()
                {
                    Name = $"{item.RdtItemId}",
                    Item = apItem.Id,
                    Priority = item.Priority.ToString().ToLowerInvariant()
                });
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
            return null;
        }
    }
}
