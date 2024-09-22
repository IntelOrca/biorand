using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static IntelOrca.Biohazard.BioRand.ScdCondition;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteFinder
    {
        private readonly Random _rng = new Random();

        public RouteFinder(int? seed = null)
        {
            if (seed != null)
                _rng = new Random(seed.Value);
        }

        public Route Find(Graph input)
        {
            var state = new State(input);
            state = DoSubgraph(state, input.Start, first: true, _rng);
            return GetRoute(state);
        }

        private static Route GetRoute(State state)
        {
            return new Route(
                state.Input,
                state.Next.Count == 0,
                state.ItemToKey,
                string.Join("\n", state.Log));
        }

        private static State DoSubgraph(State state, IEnumerable<Node> start, bool first, Random rng)
        {
            var keys = new List<Node>();
            var visited = new List<Node>();
            var next = new List<Node>();
            var toVisit = new List<Node>();
            foreach (var n in start)
            {
                var deps = GetHardDependencies(state, n);
                keys.AddRange(deps.Where(x => x.IsKey));
                visited.AddRange(deps.Where(x => !x.IsKey));
                if (first)
                    next.Add(n);
                else
                    toVisit.Add(n);
            }

            state = state.AddLog($"Begin subgraph {start.First()}");
            state = state.Clear(visited, keys, next);
            foreach (var v in toVisit)
                state = state.VisitNode(v);

            return Fulfill(state, rng);
        }

        private static State Fulfill(State state, Random rng)
        {
            state = Expand(state);
            if (!ValidateState(state))
                return state;

            // Choose a door to open
            var bestState = state;
            foreach (var n in Shuffle(rng, state.Next))
            {
                var required = GetRequiredKeys2(state, n);

                // TODO do something better here
                for (int retries = 0; retries < 10; retries++)
                {
                    var slots = FindAvailableSlots(rng, state, required);
                    if (slots == null)
                        continue;

                    var newState = state;
                    for (var i = 0; i < required.Count; i++)
                    {
                        newState = newState.PlaceKey(slots[i], required[i]);
                    }

                    var finalState = Fulfill(newState, rng);
                    if (finalState.Next.Count == 0 && finalState.OneWay.Count == 0)
                    {
                        return finalState;
                    }
                    else if (finalState.ItemToKey.Count > bestState.ItemToKey.Count)
                    {
                        bestState = finalState;
                    }
                }
            }
            return DoNextSubGraph(bestState, rng);
        }

        private static State Expand(State state)
        {
            while (true)
            {
                var (newState, satisfied) = TakeNextNodes(state);
                if (satisfied.Length == 0)
                    break;

                foreach (var n in satisfied)
                {
                    if (n.Kind == NodeKind.OneWay)
                    {
                        newState = newState.AddOneWay(n);
                    }
                    else
                    {
                        newState = newState.VisitNode(n);
                    }
                }
                state = newState;
            }
            return state;
        }

        private static List<Node> GetRequiredKeys2(State state, Node node)
        {
            var required = GetMissingKeys(state, state.Keys, node);
            var newKeys = state.Keys.AddRange(required);
            foreach (var n in state.Next)
            {
                if (n == node)
                    continue;

                var missingKeys = GetMissingKeys(state, newKeys, n);
                if (missingKeys.Count == 0)
                {
                    missingKeys = GetMissingKeys(state, state.Keys, n);
                    foreach (var k in missingKeys)
                    {
                        if (k.Kind == NodeKind.ConsumableKey)
                        {
                            required.Add(k);
                        }
                    }
                }
            }

            return required.ToList();
        }

        private static List<Node> GetMissingKeys(State state, ImmutableMultiSet<Node> keys, Node node)
        {
            var requiredKeys = node.Requires
                .Where(x => x.IsKey)
                .GroupBy(x => x)
                .ToArray();

            var required = new List<Node>();
            foreach (var g in requiredKeys)
            {
                var have = keys.GetCount(g.Key);
                var need = g.Key.Kind == NodeKind.RemovableKey
                    ? GetRemovableKeyCount(state, g.Key, node)
                    : g.Count();
                need -= have;
                for (var i = 0; i < need; i++)
                {
                    required.Add(g.Key);
                }
            }

            return required;
        }

        private static Node[]? FindAvailableSlots(Random rng, State state, List<Node> keys)
        {
            if (state.SpareItems.Count < keys.Count)
                return null;

            var available = Shuffle(rng, state.SpareItems).ToList();
            var result = new Node[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                var found = false;
                for (var j = 0; j < available.Count; j++)
                {
                    if (available[j].Group == keys[i].Group)
                    {
                        result[i] = available[j];
                        available.RemoveAt(j);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return null;
            }
            return result;
        }

        private static State DoNextSubGraph(State state, Random rng)
        {
            var subGraphs = state.OneWay.ToArray();
            foreach (var n in subGraphs)
            {
                state = DoSubgraph(state, new[] { n }, first: false, rng);
            }
            return state;
        }

        private static (State, Node[]) TakeNextNodes(State state)
        {
            var result = new List<Node>();
            while (true)
            {
                var next = state.Next.ToArray();
                var index = Array.FindIndex(next, x => IsSatisfied(state, x));
                if (index == -1)
                    break;

                var node = next[index];
                result.Add(node);

                // Remove any keys from inventory if they are consumable
                var consumableKeys = node.Requires
                    .Where(x => x.Kind == NodeKind.ConsumableKey)
                    .ToArray();
                state = state.UseKey(node, consumableKeys);
            }
            return (state, result.ToArray());
        }

        private static int GetRemovableKeyCount(State state, Node key, Node node)
        {
            var count = 0;
            Recurse(node);
            return count;

            void Recurse(Node n)
            {
                foreach (var r in n.Requires)
                {
                    if (r == key)
                        count++;
                    else
                        Recurse(r);
                }
            }
        }

        private static HashSet<Node> GetHardDependencies(State state, Node node)
        {
            var set = new HashSet<Node>();
            Recurse(node);
            return set;

            void Recurse(Node node)
            {
                foreach (var r in node.Requires)
                {
                    if (r.IsKey)
                    {
                        var items = state.ItemToKey.GetKeysContainingValue(r);
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

        private static ChecklistItem GetChecklistItem(State state, Node node)
        {
            var haveList = new List<Node>();
            var missingList = new List<Node>();
            var requiredKeys = GetRequiredKeys(state, node)
                .GroupBy(x => x)
                .ToArray();

            foreach (var edges in requiredKeys)
            {
                var key = edges.Key;
                var need = edges.Count();
                var have = state.Keys.GetCount(key);

                if (key.Kind == NodeKind.RemovableKey)
                {
                    need = GetRemovableKeyCount(state, key, node);
                }

                var missing = Math.Max(0, need - have);
                for (var i = 0; i < missing; i++)
                    missingList.Add(key);

                var progress = Math.Min(have, need);
                for (var i = 0; i < progress; i++)
                    haveList.Add(key);
            }

            return new ChecklistItem(node, haveList.ToImmutableArray(), missingList.ToImmutableArray());
        }

        private static bool ValidateState(State state)
        {
            var flags = RouteSolver.Default.Solve(GetRoute(state));
            return (flags & RouteSolverResult.PotentialSoftlock) == 0;
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

        private static T[] Shuffle<T>(Random rng, IEnumerable<T> items)
        {
            var result = items.ToArray();
            for (var i = 0; i < result.Length; i++)
            {
                var j = rng.Next(0, i + 1);
                var tmp = result[i];
                result[i] = result[j];
                result[j] = tmp;
            }
            return result;
        }

        private static bool IsSatisfied(State state, Node node)
        {
            if (node.Kind == NodeKind.OrGate)
            {
                return node.Requires.Any(x => state.Visited.Contains(x));
            }
            else
            {
                var checklistItem = GetChecklistItem(state, node);
                if (checklistItem.Need.Length > 0)
                    return false;

                return node.Requires
                    .Where(x => !x.IsKey)
                    .All(x => state.Visited.Contains(x));
            }
        }

        private static Node[] GetRequiredKeys(State state, Node node)
        {
            var leaves = new List<Node>();
            GetRequiredKeys(node);
            return leaves.ToArray();

            void GetRequiredKeys(Node c)
            {
                if (state.Visited.Contains(c))
                    return;

                foreach (var r in c.Requires)
                {
                    if (r.IsKey)
                    {
                        leaves.Add(r);
                    }
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

        private sealed class State
        {
            public Graph Input { get; }
            public ImmutableHashSet<Node> Next { get; private set; } = ImmutableHashSet<Node>.Empty;
            public ImmutableHashSet<Node> OneWay { get; private set; } = ImmutableHashSet<Node>.Empty;
            public ImmutableHashSet<Node> SpareItems { get; private set; } = ImmutableHashSet<Node>.Empty;
            public ImmutableHashSet<Node> Visited { get; private set; } = ImmutableHashSet<Node>.Empty;
            public ImmutableMultiSet<Node> Keys { get; private set; } = ImmutableMultiSet<Node>.Empty;
            public ImmutableOneToManyDictionary<Node, Node> ItemToKey { get; private set; } = ImmutableOneToManyDictionary<Node, Node>.Empty;
            public ImmutableList<string> Log { get; private set; } = ImmutableList<string>.Empty;

            public State(Graph input)
            {
                Input = input;
            }

            private State(State state)
            {
                Input = state.Input;
                Next = state.Next;
                OneWay = state.OneWay;
                SpareItems = state.SpareItems;
                Visited = state.Visited;
                Keys = state.Keys;
                ItemToKey = state.ItemToKey;
                Log = state.Log;
            }

            public State Clear(IEnumerable<Node> visited, IEnumerable<Node> keys, IEnumerable<Node> next)
            {
                var result = new State(this);
                result.Visited = ImmutableHashSet<Node>.Empty.Union(visited);
                result.Keys = ImmutableMultiSet<Node>.Empty.AddRange(keys);
                result.Next = ImmutableHashSet<Node>.Empty.Union(next);
                result.OneWay = ImmutableHashSet<Node>.Empty;
                result.SpareItems = ImmutableHashSet<Node>.Empty;
                return result;
            }

            public State AddOneWay(Node node)
            {
                var result = new State(this);
                result.OneWay = OneWay.Add(node);
                return result;
            }

            public State VisitNode(Node node)
            {
                var result = new State(this);
                result.Visited = Visited.Add(node);
                if (node.Kind == NodeKind.Item)
                {
                    if (ItemToKey.TryGetValue(node, out var key))
                    {
                        result.Keys = Keys.Add(key);
                    }
                    else
                    {
                        result.SpareItems = SpareItems.Add(node);
                    }
                }
                if (!node.IsKey)
                {
                    result.Next = Next.Union(Input.GetEdges(node));
                }
                result.Log = Log.Add($"Satisfied node: {node}");
                return result;
            }

            public State PlaceKey(Node item, Node key)
            {
                var result = new State(this);
                result.SpareItems = SpareItems.Remove(item);
                result.ItemToKey = ItemToKey.Add(item, key);
                result.Keys = Keys.Add(key);
                result.Log = Log.Add($"Place {key} at {item}");
                return result;
            }

            public State UseKey(Node unlock, params Node[] keys)
            {
                var result = new State(this);
                result.Next = Next.Remove(unlock);
                result.Keys = Keys.RemoveMany(keys);
                return result;
            }

            public State AddLog(string message)
            {
                var state = new State(this);
                state.Log = Log.Add(message);
                return state;
            }
        }
    }
}
