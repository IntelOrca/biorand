using System.Collections.Generic;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public sealed class Route
    {
        public Graph Graph { get; }
        public bool AllNodesVisited { get; }
        public ImmutableDictionary<Node, Node> ItemToKey { get; }
        public ImmutableDictionary<Node, ImmutableArray<Node>> KeyToItems { get; }
        public string Log { get; }

        public Route(
            Graph graph,
            bool allNodesVisited,
            ImmutableDictionary<Node, Node> itemToKey,
            ImmutableDictionary<Node, ImmutableArray<Node>> keyToItems,
            string log)
        {
            Graph = graph;
            AllNodesVisited = allNodesVisited;
            ItemToKey = itemToKey;
            KeyToItems = keyToItems;
            Log = log;
        }

        public Node? GetItemContents(Node item)
        {
            if (ItemToKey.TryGetValue(item, out var result))
                return result;
            return null;
        }

        public string GetDependencyTree(Node node, bool keysAsNodes = false)
        {
            var visited = new HashSet<Node>();
            var mb = new MermaidBuilder();
            Visit(node);
            return mb.ToString();

            void Visit(Node n)
            {
                if (!visited.Add(n))
                    return;

                if (keysAsNodes || n.Kind != NodeKind.Key)
                {
                    var label = n.Label;
                    if (n.Kind == NodeKind.Item && !keysAsNodes)
                    {
                        if (ItemToKey.TryGetValue(n, out var key))
                        {
                            label += $"\n<small>{key}</small>";
                        }
                    }
                    mb.Node($"N{n.Id}", label,
                        n.Kind switch
                        {
                            NodeKind.Key => MermaidShape.Hexagon,
                            NodeKind.Item => keysAsNodes
                                ? MermaidShape.Square
                                : MermaidShape.DoubleSquare,
                            _ => MermaidShape.Circle,
                        });
                }
                if (n.Kind == NodeKind.Key)
                {
                    if (KeyToItems.TryGetValue(n, out var items))
                    {
                        foreach (var item in items)
                        {
                            Visit(item);
                            if (keysAsNodes)
                                mb.Edge($"N{item.Id}", $"N{n.Id}",
                                    type: items.Length == 1 ? MermaidEdgeType.Solid : MermaidEdgeType.Dotted);
                        }
                    }
                }
                else
                {
                    foreach (var r in n.Requires)
                    {
                        Visit(r);
                        if (r.Kind == NodeKind.Key && !keysAsNodes)
                        {
                            if (KeyToItems.TryGetValue(r, out var items))
                            {
                                foreach (var item in items)
                                {
                                    Visit(item);
                                    mb.Edge($"N{item.Id}", $"N{n.Id}",
                                        type: items.Length == 1 ? MermaidEdgeType.Solid : MermaidEdgeType.Dotted);
                                }
                            }
                        }
                        else
                        {
                            mb.Edge($"N{r.Id}", $"N{n.Id}");
                        }
                    }
                }
            }
        }
    }
}
