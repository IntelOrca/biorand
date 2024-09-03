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
            if (node.Kind == NodeKind.Key)
                return new Node[] { node };
            return node.Requires
                .SelectMany(x => GetKeys(x))
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

            var sb = new StringBuilder();
            sb.AppendLine("flowchart TD");

            for (int gIndex = 0; gIndex < Subgraphs.Length; gIndex++)
            {
                var g = Subgraphs[gIndex];
                sb.AppendLine($"        subgraph \"G<sub>{gIndex}\"");
                foreach (var node in g)
                {
                    if (node.Kind == NodeKind.Key && !keysAsNodes)
                        continue;

                    sb.AppendLine($"        {GetNodeLabel(node)}");
                }
                sb.AppendLine($"    end");
            }

            foreach (var edge in Edges)
            {
                var a = edge.Key;
                if (a.Kind == NodeKind.Key && !keysAsNodes)
                    continue;

                var source = GetNodeName(a);
                foreach (var b in edge.Value)
                {
                    var target = GetNodeName(b);

                    var keys = GetKeys(b);
                    var keyString = string.Join(" + ", keys.Select(k => $"K<sub>{k.Id}</sub>"));
                    var line = keys.Length == 0
                        ? b.Kind == NodeKind.OneWay
                            ? "-..->"
                            : "-->"
                        : b.Kind == NodeKind.OneWay
                            ? $"-. {keyString} .->"
                            : $"-- {keyString} -->";
                    sb.AppendLine($"    {source} {line} {target}");
                }
            }
            return sb.ToString();

            static string GetNodeName(Node node)
            {
                return $"N{node.Id}";
            }

            static string GetNodeLabel(Node node)
            {
                var paren = node.Kind switch
                {
                    NodeKind.AndGate => "R()",
                    NodeKind.OrGate => "R()",
                    NodeKind.OneWay => "R()",
                    NodeKind.Item => "I[]",
                    NodeKind.Key => "K{{}}",
                    _ => "R()"
                };
                var letter = paren[0];
                paren = paren[1..];
                var parenOpen = paren[..(paren.Length / 2)];
                var parenClose = paren[(paren.Length / 2)..];
                return $"{GetNodeName(node)}{parenOpen}\"{letter}<sub>{node.Id}</sub>\"{parenClose}";
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
        public bool AllNodesVisited { get; }
        public ImmutableDictionary<Node, Node> Fulfilled { get; }
        public string Log { get; }

        public Route(bool allNodesVisited, ImmutableDictionary<Node, Node> fulfilled, string log)
        {
            AllNodesVisited = allNodesVisited;
            Fulfilled = fulfilled;
            Log = log;
        }

        public Node? GetGetNodeForKey(Node key)
        {
            if (Fulfilled.TryGetValue(key, out var result))
                return result;
            return null;
        }
    }

    public class RouteFinder
    {
        private readonly Random _rng = new Random();

        private Graph _input;
        private readonly HashSet<Node> _next = new HashSet<Node>();

        private readonly HashSet<Node> _remainingNodes = new HashSet<Node>();
        private readonly HashSet<Node> _remainingKeys = new HashSet<Node>();
        private readonly HashSet<Node> _availableForKey = new HashSet<Node>();
        private readonly List<Node> _visited = new List<Node>();
        private readonly HashSet<Node> _keys = new HashSet<Node>();
        private readonly StringBuilder _log = new StringBuilder();
        private readonly Dictionary<Node, Node> _fulfilled = new Dictionary<Node, Node>();

        public RouteFinder(int? seed = null)
        {
            if (seed != null)
                _rng = new Random(seed.Value);
        }

        public Route Find(Graph input)
        {
            _input = input;
            _next.AddRange(input.Start);
            while (DoPass() || Fulfill())
            {
            }
            return new Route(_next.Count == 0, _fulfilled.ToImmutableDictionary(), _log.ToString());
        }

        private bool DoPass()
        {
            var satisfied = _next.Where(x => IsSatisfied(x)).ToArray();
            if (satisfied.Length == 0)
                return false;

            _next.ExceptWith(satisfied);
            foreach (var c in satisfied)
            {
                VisitNode(c);
            }

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
                    _fulfilled.Add(key, item);
                    _keys.Remove(key);
                    VisitNode(key);
                    return true;
                }
            }
            return false;
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
