using System;
using System.Collections.Generic;
using System.Linq;
using OpenSoftware.DgmlTools;
using OpenSoftware.DgmlTools.Builders;
using OpenSoftware.DgmlTools.Model;

namespace IntelOrca.Biohazard.BioRand
{
    internal class PlayGraph
    {
        public PlayNode? Start { get; set; }
        public PlayNode? End { get; set; }

        public void GenerateDgml(string path, IItemHelper itemHelper)
        {
            var builder = new DgmlBuilder()
            {
                NodeBuilders = new[]
                {
                    new NodeBuilder<PlayNode>(n => GetNode(n, itemHelper))
                },
                LinkBuilders = new LinkBuilder[]
                {
                    new LinksBuilder<PlayNode>(n => GetLinksForNode(n, itemHelper))
                },
                CategoryBuilders = new CategoryBuilder[0],
                StyleBuilders = new StyleBuilder[0]
            };
            var dgmlGraph = builder.Build(GetAllNodes());
            dgmlGraph.WriteToFile(path);
        }

        private Node GetNode(PlayNode node, IItemHelper itemHelper)
        {
            var label = node.RdtId.ToString();
            foreach (var keyItem in node.PlacedKeyItems)
            {
                label += "\n" + itemHelper.GetItemName((byte)keyItem.Type) + " x" + keyItem.Amount;
            }
            var itemsLeft = node.Items.Length - node.PlacedKeyItems.Count;
            if (itemsLeft > 0)
            {
                var potentialKeyItems = node.Items.Count(x => x.Priority == ItemPriority.Normal && (x.Requires?.Length ?? 0) == 0) - node.PlacedKeyItems.Count;
                label += $"\n{potentialKeyItems}/{node.Items.Length - node.PlacedKeyItems.Count} other items";
            }

            var result = new Node()
            {
                Id = node.RdtId.ToString(),
                Label = label
            };

            if (node == Start || node == End)
                result.Properties["Background"] = "LightBlue";
            if (node.Category == DoorRandoCategory.Box)
                result.Properties["Background"] = "Green";
            if (node.Category == DoorRandoCategory.Bridge)
                result.Properties["Background"] = "Orange";
            if (node.Category == DoorRandoCategory.Segment)
                result.Properties["Background"] = "Red";
            return result;
        }

        private List<Link> GetLinksForNode(PlayNode node, IItemHelper itemHelper)
        {
            var links = new List<Link>();
            foreach (var edge in node.Edges)
            {
                if (!IsLinkAccessible(edge))
                    continue;

                var oppositeEdge = edge.Node!.Edges.FirstOrDefault(x => x.Node == node);
                if (oppositeEdge != null && IsLinkAccessible(oppositeEdge) && edge.Node.Depth < node.Depth)
                    continue;

                var label = edge.DoorId.ToString();
                if (edge.NoReturn)
                {
                    label += $"\n(no return)";
                }
                if (edge.Lock == LockKind.None)
                {
                    if (oppositeEdge != null && oppositeEdge.Lock == LockKind.Side)
                    {
                        label += $"\n(locked)";
                    }
                }
                else
                {
                    label += $"\n({edge.Lock.ToString().ToLower()})";
                }
                foreach (var key in edge.Requires)
                {
                    label += $"\n[{itemHelper.GetItemName((byte)key)}]";
                }

                links.Add(new Link()
                {
                    Source = node.RdtId.ToString(),
                    Target = edge.Node.RdtId.ToString(),
                    Label = label
                });
            }
            return links;
        }

        private static bool IsLinkAccessible(PlayEdge edge)
        {
            if (edge.Node == null)
                return false;
            if (edge.Lock != LockKind.None && edge.Lock != LockKind.Unblock)
                return false;
            return true;
        }

        public PlayNode[] GetAllNodes()
        {
            var nodes = new HashSet<PlayNode>();
            if (Start != null)
                AddNodeAndEdges(nodes, Start);
            if (End != null)
                AddNodeAndEdges(nodes, End);
            return nodes.ToArray();
        }

        private void AddNodeAndEdges(HashSet<PlayNode> nodes, PlayNode node)
        {
            if (nodes.Add(node))
            {
                foreach (var edge in node.Edges)
                {
                    if (edge.Node != null)
                    {
                        AddNodeAndEdges(nodes, edge.Node);
                    }
                }
            }
        }

        public RandomizedRdt[] GetAccessibleRdts(GameData gameData)
        {
            if (Start == null)
                return gameData.Rdts;

            var visited = new HashSet<PlayNode>();
            var q = new Queue<PlayNode>();
            q.Enqueue(Start);
            while (q.Count > 0)
            {
                var node = q.Dequeue();
                if (visited.Add(node))
                {
                    foreach (var e in node.Edges)
                    {
                        if (e.Node != null)
                        {
                            q.Enqueue(e.Node);
                        }
                    }
                }
            }

            var result = visited
                .Select(x => gameData.GetRdt(x.RdtId)!)
                .ToHashSet();

            // Add linked RDTs as well
            foreach (var v in visited)
            {
                if (v.LinkedRdtId != null)
                {
                    result.Add(gameData.GetRdt(v.LinkedRdtId.Value)!);
                }
            }

            return result
                .OrderBy(x => x.RdtId)
                .ToArray();
        }
    }

    internal class PlayNode
    {
        public RdtId RdtId { get; set; }
        public RdtId? LinkedRdtId { get; set; }
        public ItemPoolEntry[] Items { get; set; } = Array.Empty<ItemPoolEntry>();
        public Dictionary<byte, RdtItemId> LinkedItems { get; set; } = new Dictionary<byte, RdtItemId>();
        public byte[] Requires { get; set; } = Array.Empty<byte>();
        public PlayNode[] RequiresRoom { get; set; } = Array.Empty<PlayNode>();
        public List<PlayEdge> Edges { get; } = new List<PlayEdge>();
        public byte[] DoorRandoAllRequiredItems { get; set; } = Array.Empty<byte>();
        public DoorRandoCategory Category { get; set; }
        public int[] DoorRandoNop { get; set; } = Array.Empty<int>();
        public List<ItemPoolEntry> PlacedKeyItems { get; } = new List<ItemPoolEntry>();
        public int Depth { get; set; }
        public bool Visited { get; set; }
        public bool HasCutscene { get; set; }

        public PlayNode(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public override string ToString() => RdtId.ToString();
    }

    internal enum LockKind
    {
        None,
        Always,
        Side,
        Gate,
        Unblock
    }

    internal enum DoorRandoCategory
    {
        Include,
        Exclude,
        Static,
        Bridge,
        Box,
        Segment,
    }

    internal class PlayEdge
    {
        public PlayNode Parent { get; }
        public RdtId OriginalTargetRdt { get; set; }
        public PlayNode? Node { get; set; }
        public LockKind Lock { get; set; }
        public byte LockId { get; set; }
        public bool NoReturn { get; set; }
        public byte[] Requires { get; set; }
        public PlayNode[] RequiresRoom { get; set; } = Array.Empty<PlayNode>();
        public int? DoorId { get; }
        public DoorEntrance? Entrance { get; set; }
        public bool PreventLoopback { get; set; }
        public bool Randomize { get; set; } = true;
        public bool NoUnlock { get; set; }
        public bool IsBridgeEdge { get; set; }
        public MapRoomDoor Raw { get; set; }

        public PlayEdge(PlayNode parent, PlayNode node, bool noReturn, byte[]? requires, int? doorId, DoorEntrance? entrance, MapRoomDoor raw)
        {
            Parent = parent;
            OriginalTargetRdt = node.RdtId;
            Node = node;
            NoReturn = noReturn;
            Requires = requires ?? new byte[0];
            DoorId = doorId;
            Entrance = entrance;
            Raw = raw;
        }

        public string GetRequiresString(IItemHelper? itemHelper)
        {
            var s = "";
            if (Lock == LockKind.Side)
            {
                s += $"(locked #{LockId}) ";
            }
            else if (Lock != LockKind.None)
            {
                s += $"({Lock.ToString().ToLowerInvariant()}) ";
            }
            if (Requires != null && Requires.Length != 0)
            {
                s += "[" + string.Join(", ", Requires.Select(x => GetItemName(itemHelper, (byte)x) ?? x.ToString())) + "] ";
            }
            return s.Trim();
        }

        private string GetItemName(IItemHelper? itemHelper, byte item)
        {
            if (itemHelper == null)
                return item.ToString();
            return itemHelper.GetItemName(item);
        }

        public override string ToString()
        {
            if (Node == null)
                return $"{Parent} -> (unconnected)";

            return $"{Parent} -> {Node.RdtId} {GetRequiresString(null)}";
        }
    }
}
