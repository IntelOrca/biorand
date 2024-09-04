using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.BioRand.Routing
{
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

            return new Route(
                _input,
                _next.Count == 0,
                _itemToKey.ToImmutableDictionary(),
                _keyToItems.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutableArray()),
                _log.ToString());
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
