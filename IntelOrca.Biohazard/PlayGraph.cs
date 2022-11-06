using System;
using System.Collections.Generic;
using System.Linq;
using OpenSoftware.DgmlTools;
using OpenSoftware.DgmlTools.Builders;
using OpenSoftware.DgmlTools.Model;

namespace IntelOrca.Biohazard
{
    internal class PlayGraph
    {
        public PlayNode? Start { get; set; }
        public PlayNode? End { get; set; }

        public void GenerateDgml(string path)
        {
            var builder = new DgmlBuilder()
            {
                NodeBuilders = new[]
                {
                    new NodeBuilder<PlayNode>(node =>
                    {
                        var label = node.RdtId.ToString();
                        foreach (var keyItem in node.PlacedKeyItems)
                        {
                            label += "\n" + Items.GetItemName(keyItem.Type) + " x" + keyItem.Amount;
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
                        return result;
                    })
                },
                LinkBuilders = new LinkBuilder[]
                {
                    new LinksBuilder<PlayNode>(node =>
                    {
                        var links = new List<Link>();
                        foreach (var edge in node.Edges)
                        {
                            if (edge.Node == null || edge.Lock == LockKind.Always || edge.Node.Depth < node.Depth)
                                continue;

                            var label = edge.DoorId.ToString();
                            if (edge.NoReturn)
                            {
                                label += $"\n(no return)";
                            }
                            foreach (var key in edge.Requires)
                            {
                                label += $"\n[{Items.GetItemName(key)}]";
                            }

                            links.Add(new Link()
                            {
                                Source = node.RdtId.ToString(),
                                Target = edge.Node.RdtId.ToString(),
                                Label = label
                            });
                        }
                        return links;
                    })
                },
                CategoryBuilders = new CategoryBuilder[0],
                StyleBuilders = new StyleBuilder[0]
            };
            var dgmlGraph = builder.Build(GetAllNodes());
            dgmlGraph.WriteToFile(path);
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
    }

    internal class PlayNode
    {
        public RdtId RdtId { get; set; }
        public ItemPoolEntry[] Items { get; set; } = Array.Empty<ItemPoolEntry>();
        public Dictionary<byte, RdtItemId> LinkedItems { get; set; } = new Dictionary<byte, RdtItemId>();
        public ushort[] Requires { get; set; } = Array.Empty<ushort>();
        public List<PlayEdge> Edges { get; } = new List<PlayEdge>();
        public ushort[] DoorRandoAllRequiredItems { get; set; } = Array.Empty<ushort>();
        public DoorRandoCategory Category { get; set; }
        public int[] DoorRandoNop { get; set; } = Array.Empty<int>();
        public List<ItemPoolEntry> PlacedKeyItems { get; } = new List<ItemPoolEntry>();
        public int Depth { get; set; }
        public bool Visited { get; set; }

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
        Box
    }

    internal class PlayEdge
    {
        public PlayNode Parent { get; }
        public RdtId OriginalTargetRdt { get; set; }
        public PlayNode? Node { get; set; }
        public LockKind Lock { get; set; }
        public bool NoReturn { get; set; }
        public ushort[] Requires { get; }
        public PlayNode[] RequiresRoom { get; set; } = Array.Empty<PlayNode>();
        public int? DoorId { get; }
        public DoorEntrance? Entrance { get; set; }
        public bool PreventLoopback { get; set; }
        public bool Randomize { get; set; } = true;

        public PlayEdge(PlayNode parent, PlayNode node, bool noReturn, ushort[]? requires, int? doorId, DoorEntrance? entrance)
        {
            Parent = parent;
            OriginalTargetRdt = node.RdtId;
            Node = node;
            NoReturn = noReturn;
            Requires = requires ?? new ushort[0];
            DoorId = doorId;
            Entrance = entrance;
        }

        public string RequiresString
        {
            get
            {
                var s = "";
                if (Lock != LockKind.None)
                {
                    s += $"({Lock.ToString().ToLowerInvariant()}) ";
                }
                if (Requires != null && Requires.Length != 0)
                {
                    s += "[" + string.Join(", ", Requires.Select(x => Items.GetItemName(x))) + "] ";
                }
                return s.Trim();
            }
        }

        public override string ToString()
        {
            if (Node == null)
                return "(unconnected)";

            return string.Join(" ", Node.RdtId, RequiresString);
        }
    }
}
