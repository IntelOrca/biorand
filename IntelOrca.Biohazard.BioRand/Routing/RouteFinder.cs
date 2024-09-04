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
        private readonly HashSet<Node> _spareItems = new HashSet<Node>();
        private readonly HashSet<Node> _visited = new HashSet<Node>();
        private readonly MultiSet<Node> _keys = new MultiSet<Node>();
        private readonly StringBuilder _log = new StringBuilder();
        private readonly OneToManyDictionary<Node, Node> _itemToKey = new OneToManyDictionary<Node, Node>();

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
                _itemToKey.ToImmutable(),
                _log.ToString());
        }

        private void DoSubgraph(IEnumerable<Node> start)
        {
            _visited.Clear();
            _keys.Clear();
            _start.Clear();
            _start.AddRange(start);
            foreach (var n in start)
            {
                var deps = GetHardDependencies(n);
                _keys.AddRange(deps.Where(x => x.Kind == NodeKind.Key));
                _visited.UnionWith(deps.Where(x => x.Kind != NodeKind.Key));
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
            var checklist = GetChecklist();
            var requiredKeys = Shuffle(checklist.SelectMany(x => x.Need).Distinct());
            foreach (var key in requiredKeys)
            {
                // Find an item for this key
                var available = Shuffle(_spareItems.Where(x => x.Group == key.Group));
                if (available.Length != 0)
                {
                    var rIndex = Rng(0, available.Length);
                    var item = available[rIndex];

                    _spareItems.Remove(item);
                    _itemToKey.Add(item, key);

                    Log($"Place {key} at {item}");

                    _keys.Add(key);
                    // VisitNode(key);
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

                _spareItems.Add(node);
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
                        var items = _itemToKey.GetKeysContainingValue(r);
                        if (items.Any())
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

        private ImmutableArray<ChecklistItem> GetChecklist()
        {
            return _next.Select(GetChecklistItem).ToImmutableArray();
        }

        private ChecklistItem GetChecklistItem(Node node)
        {
            var haveList = new List<Node>();
            var missingList = new List<Node>();
            var requiredKeys = GetRequiredKeys(node)
                .GroupBy(x => x)
                .Select(x => (x.Key, x.Count()))
                .ToArray();

            foreach (var (key, need) in requiredKeys)
            {
                var have = _keys.GetCount(key);

                var missing = Math.Max(0, need - have);
                for (var i = 0; i < missing; i++)
                    missingList.Add(key);

                var progress = Math.Min(have, need);
                for (var i = 0; i < progress; i++)
                    haveList.Add(key);
            }

            return new ChecklistItem(node, haveList.ToImmutableArray(), missingList.ToImmutableArray());
        }

        private sealed class ChecklistItem
        {
            public Node Destination { get; }
            public ImmutableArray<Node> Have { get; }
            public ImmutableArray<Node> Need { get; }

            public ChecklistItem(Node destination, ImmutableArray<Node> have, ImmutableArray<Node> need)
            {
                Destination = destination;
                Have = have;
                Need = need;
            }

            public override string ToString() => string.Format("{0} Have = {{{1}}} Need = {{{2}}}",
                Destination, string.Join(", ", Have), string.Join(", ", Need));
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
                var checklistItem = GetChecklistItem(node);
                if (checklistItem.Need.Length > 0)
                    return false;

                return node.Requires
                    .Where(x => x.Kind != NodeKind.Key)
                    .All(x => _visited.Contains(x));
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
