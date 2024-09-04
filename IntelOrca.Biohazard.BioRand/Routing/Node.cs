using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public enum NodeKind : byte
    {
        AndGate,
        OrGate,
        OneWay,
        Item,
        Key,
        Removable,
        Consume,
    }

    public readonly struct Node : IEquatable<Node>
    {
        public int Id { get; }
        public int Group { get; }
        public NodeKind Kind { get; }
        public string? Label { get; }
        public Node[] Requires { get; }

        public Node(int id, int group, NodeKind flags, string? label, Node[] requires)
        {
            Id = id;
            Group = group;
            Kind = flags;
            Label = label;
            Requires = requires;
        }

        public bool Equals(Node other) => Id == other.Id;
        public override string ToString() => $"#{Id} ({Label})" ?? $"#{Id}";

        public static bool operator ==(Node a, Node b) => a.Equals(b);
        public static bool operator !=(Node a, Node b) => !a.Equals(b);
    }

    public sealed class Graph
    {
        public ImmutableArray<Node> Nodes { get; }
        public ImmutableDictionary<Node, ImmutableArray<Node>> Edges { get; }
        public ImmutableArray<Node> Start { get; }
        public ImmutableArray<ImmutableArray<Node>> Subgraphs { get; }

        public Graph(
            ImmutableArray<Node> nodes,
            ImmutableDictionary<Node, ImmutableArray<Node>> edges)
        {
            Nodes = nodes;
            Edges = edges;
            Start = nodes
                .Where(IsStartNode)
                .ToImmutableArray();
            Subgraphs = GetSubgraphs();
        }

        public ImmutableArray<Node> GetEdges(Node node)
        {
            if (Edges.TryGetValue(node, out var edges))
                return edges;
            return ImmutableArray<Node>.Empty;
        }

        private Node[] GetKeys(Node node)
        {
            return node.Requires
                .Where(x => x.Kind == NodeKind.Key)
                .ToArray();
        }

        private ImmutableArray<ImmutableArray<Node>> GetSubgraphs()
        {
            var graphs = new List<ImmutableArray<Node>>();
            var visited = new HashSet<Node>();
            var next = Start as IEnumerable<Node>;
            while (next.Any())
            {
                var (g, end) = GetEndNodes(next);
                graphs.Add(g.ToImmutableArray());
                next = end;
            }
            return graphs.ToImmutableArray();

            (Node[], Node[]) GetEndNodes(IEnumerable<Node> start)
            {
                var nodes = new List<Node>();
                var end = new List<Node>();
                var q = new Queue<Node>(start);
                while (q.Count != 0)
                {
                    var n = q.Dequeue();
                    visited.Add(n);
                    nodes.Add(n);

                    var edges = GetEdges(n);
                    foreach (var e in edges)
                    {
                        if (e.Kind == NodeKind.OneWay)
                        {
                            end.Add(e);
                        }
                        else
                        {
                            q.Enqueue(e);
                        }
                    }
                }
                return (nodes.ToArray(), end.Where(x => !visited.Contains(x)).ToArray());
            }
        }

        private static bool IsStartNode(Node node) => RequiresNothingOrKeys(node) && node.Kind != NodeKind.Key;
        private static bool RequiresNothingOrKeys(Node node) => node.Requires.All(x => x.Kind == NodeKind.Key);

        public string ToMermaid()
        {
            var keysAsNodes = false;

            var mb = new MermaidBuilder();
            mb.Node("S", " ", MermaidShape.Circle);
            for (int gIndex = 0; gIndex < Subgraphs.Length; gIndex++)
            {
                var g = Subgraphs[gIndex];
                mb.BeginSubgraph($"G<sub>{gIndex}</sub>");
                foreach (var node in g)
                {
                    if (node.Kind == NodeKind.Key && !keysAsNodes)
                        continue;

                    var (letter, shape) = GetNodeLabel(node);
                    mb.Node(GetNodeName(node), $"{letter}<sub>{node.Id}</sub>", shape);
                }
                mb.EndSubgraph();
            }

            foreach (var node in Start)
            {
                EmitEdge("S", node);
            }

            foreach (var edge in Edges)
            {
                var a = edge.Key;
                if (a.Kind == NodeKind.Key && !keysAsNodes)
                    continue;

                var sourceName = GetNodeName(a);
                foreach (var b in edge.Value)
                {
                    EmitEdge(sourceName, b);
                }
            }
            return mb.ToString();

            void EmitEdge(string sourceName, Node b)
            {
                var targetName = GetNodeName(b);
                var label = string.Join(" + ", GetKeys(b).Select(k => $"K<sub>{k.Id}</sub>"));
                var edgeType = b.Kind == NodeKind.OneWay
                    ? MermaidEdgeType.Dotted
                    : MermaidEdgeType.Solid;
                mb.Edge(sourceName, targetName, label, edgeType);
            }

            static string GetNodeName(Node node)
            {
                return $"N{node.Id}";
            }

            static (char, MermaidShape) GetNodeLabel(Node node)
            {
                return node.Kind switch
                {
                    NodeKind.Item => ('I', MermaidShape.Square),
                    NodeKind.Key => ('K', MermaidShape.Hexagon),
                    _ => ('R', MermaidShape.Circle),
                };
            }
        }
    }

    public class GraphBuilder
    {
        private readonly List<Node> _nodes = new List<Node>();
        private int _id;

        private int GetNextId()
        {
            return ++_id;
        }

        public Node Key(int group, string? label)
        {
            var node = new Node(GetNextId(), group, NodeKind.Key, label, new Node[0]);
            _nodes.Add(node);
            return node;
        }

        public Node Item(int group, string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), group, NodeKind.Item, label, requires.ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node AndGate(string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.AndGate, label, requires.ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node OrGate(string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.OrGate, label, requires.ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node OneWay(string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.OneWay, label, requires.ToArray());
            _nodes.Add(node);
            return node;
        }

        public Graph Build()
        {
            var edges = new Dictionary<Node, List<Node>>();
            foreach (var c in _nodes)
            {
                foreach (var r in c.Requires)
                {
                    if (!edges.TryGetValue(r, out var list))
                    {
                        list = new List<Node>();
                        edges[r] = list;
                    }
                    list.Add(c);
                }
            }

            return new Graph(
                _nodes.ToImmutableArray(),
                edges.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutableArray()));
        }

        public Route GenerateRoute(int? seed = null)
        {
            return new RouteFinder(seed).Find(Build());
        }
    }

    public sealed class Route
    {
        public Graph Graph { get; }
        public bool AllNodesVisited { get; }
        public ImmutableDictionary<Node, Node> Fulfilled { get; }
        public string Log { get; }

        public Route(Graph graph, bool allNodesVisited, ImmutableDictionary<Node, Node> fulfilled, string log)
        {
            Graph = graph;
            AllNodesVisited = allNodesVisited;
            Fulfilled = fulfilled;
            Log = log;
        }

        public Node? GetItemContents(Node item)
        {
            if (Fulfilled.TryGetValue(item, out var result))
                return result;
            return null;
        }
    }

    public class RouteFinder
    {
        private readonly Random _rng = new Random();

        private Graph _input;
        private readonly HashSet<Node> _start = new HashSet<Node>();
        private readonly HashSet<Node> _next = new HashSet<Node>();

        private readonly HashSet<Node> _remainingNodes = new HashSet<Node>();
        private readonly HashSet<Node> _remainingKeys = new HashSet<Node>();
        private readonly HashSet<Node> _availableForKey = new HashSet<Node>();
        private readonly HashSet<Node> _visited = new HashSet<Node>();
        private readonly HashSet<Node> _keys = new HashSet<Node>();
        private readonly StringBuilder _log = new StringBuilder();
        private readonly Dictionary<Node, Node> _itemToKey = new Dictionary<Node, Node>();
        private readonly Dictionary<Node, List<Node>> _keyToItems = new Dictionary<Node, List<Node>>();

        public RouteFinder(int? seed = null)
        {
            if (seed != null)
                _rng = new Random(seed.Value);
        }

        public Route Find(Graph input)
        {
            _input = input;
            var m = input.ToMermaid();

            var next = (IEnumerable<Node>)input.Start;
            while (next.Any())
            {
                DoSubgraph(next);
                next = TakeNextNodes(IsSatisfied);
            }

            return new Route(_input, _next.Count == 0, _itemToKey.ToImmutableDictionary(), _log.ToString());
        }

        private void DoSubgraph(IEnumerable<Node> start)
        {
            _visited.Clear();
            _start.Clear();
            _start.AddRange(start);
            foreach (var n in start)
            {
                var deps = GetHardDependencies(n);
                _visited.UnionWith(deps);
                _next.Add(n);
            }
            while (DoPass() || Fulfill())
            {
            }
        }

        private bool DoPass()
        {
            var satisfied = TakeNextNodes(x => (_start.Contains(x) || x.Kind != NodeKind.OneWay) && IsSatisfied(x));
            if (satisfied.Length == 0)
                return false;

            VisitNodes(satisfied);
            return true;
        }

        private bool Fulfill()
        {
            var nextKeys = _next
                .SelectMany(x => GetRequiredKeys(x))
                .ToArray();
            _keys.AddRange(nextKeys);

            var keys = Shuffle(_keys);
            foreach (var key in keys)
            {
                // Find an item for this key
                var available = Shuffle(_availableForKey.Where(x => x.Group == key.Group));
                if (available.Length != 0)
                {
                    var rIndex = Rng(0, available.Length);
                    var item = available[rIndex];

                    _availableForKey.Remove(item);
                    _itemToKey.Add(item, key);

                    if (!_keyToItems.TryGetValue(key, out var list))
                    {
                        list = new List<Node>();
                        _keyToItems.Add(key, list);
                    }
                    list.Add(item);

                    Log($"Place {key} at {item}");

                    _keys.Remove(key);
                    VisitNode(key);
                    return true;
                }
            }
            return false;
        }

        private Node[] TakeNextNodes(Func<Node, bool> predicate)
        {
            var taken = _next.Where(predicate).ToArray();
            _next.ExceptWith(taken);
            return taken;
        }

        private void VisitNodes(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
            {
                VisitNode(node);
            }
        }

        private void VisitNode(Node node)
        {
            // Mark as visited
            _visited.Add(node);

            if (node.Kind != NodeKind.Key)
            {
                // Add edges
                _next.AddRange(_input.GetEdges(node));

                _availableForKey.Add(node);
            }

            Log($"Satisfied node: {node}");
        }

        private HashSet<Node> GetHardDependencies(Node node)
        {
            var set = new HashSet<Node>();
            Recurse(node);
            return set;

            void Recurse(Node node)
            {
                foreach (var r in node.Requires)
                {
                    if (r.Kind == NodeKind.Key)
                    {
                        if (_keyToItems.TryGetValue(r, out var items) && items.Count != 0)
                        {
                            var item = items.FirstOrDefault();
                            set.Add(r);
                            set.Add(item);
                            Recurse(item);
                        }
                    }
                    else
                    {
                        set.Add(r);
                        Recurse(r);
                    }
                }
            }
        }

        private int Rng(int min, int max) => _rng.Next(min, max);
        private T[] Shuffle<T>(IEnumerable<T> items)
        {
            var result = items.ToArray();
            for (var i = 0; i < result.Length; i++)
            {
                var j = Rng(0, i + 1);
                var tmp = result[i];
                result[i] = result[j];
                result[j] = tmp;
            }
            return result;
        }

        private void Log(string message)
        {
            _log.Append(message);
            _log.Append('\n');
        }

        private bool IsSatisfied(Node node)
        {
            if (node.Kind == NodeKind.OrGate)
            {
                return node.Requires.Any(x => _visited.Contains(x));
            }
            else
            {
                return node.Requires.All(x => _visited.Contains(x));
            }
        }

        private Node[] GetRequiredKeys(Node node)
        {
            var leaves = new List<Node>();
            GetRequiredKeys(node);
            return leaves.ToArray();

            void GetRequiredKeys(Node c)
            {
                if (_visited.Contains(c))
                    return;

                if (c.Kind == NodeKind.Key)
                    leaves.Add(c);

                foreach (var r in c.Requires)
                {
                    GetRequiredKeys(r);
                }
            }
        }

        private static void GetRequiredKeys(List<Node> list, Node node)
        {
            if (node.Requires.Length == 0)
            {
                list.Add(node);
            }
            else
            {
                foreach (var c in node.Requires)
                {
                    GetRequiredKeys(list, c);
                }
            }
        }
    }
}
