using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public class RouteSolver
    {
        public static RouteSolver Default => new RouteSolver();

        private RouteSolver()
        {
        }

        public RouteSolverResult Solve(Route route)
        {
            var state = Begin(route);
            while (true)
            {
                state = Expand(state);
                var newState = UseKey(state);
                if (newState == null)
                    return RouteSolverResult.PotentialSoftlock | RouteSolverResult.NodesRemaining;
                if (newState == state)
                    break;
                state = newState;
            }

            RouteSolverResult flags = 0;
            if (state.Next.Count != 0)
                flags |= RouteSolverResult.NodesRemaining;
            return flags;
        }

        private static State Begin(Route route)
        {
            return new State(
                route,
                ImmutableHashSet.CreateRange(route.Graph.Start),
                ImmutableHashSet.CreateRange(route.Graph.Start.SelectMany(x => route.Graph.GetEdges(x))),
                ImmutableMultiSet<Node>.Empty);
        }

        private static State Expand(State state)
        {
            var graph = state.Route.Graph;
            var newVisits = new List<Node>();
            do
            {
                newVisits.Clear();
                foreach (var node in state.Next)
                {
                    if (!node.Requires.All(x => state.Visited.Contains(x.Node)))
                        continue;

                    newVisits.Add(node);
                }
                state = state.Visit(newVisits);
            } while (newVisits.Count != 0);
            return state;
        }

        private static State? UseKey(State state)
        {
            var graph = state.Route.Graph;
            var possibleWays = state.Next
                .Where(x => HasAllKeys(state, x))
                .ToArray();

            // Lets first unlock anything that doesn't consume a key
            var safeWays = possibleWays
                .Where(x => x.Requires.All(x => (x.Flags & EdgeFlags.Consume) == 0))
                .ToArray();
            if (safeWays.Length != 0)
            {
                foreach (var way in safeWays)
                {
                    state = state.Visit(way);
                }
                return state;
            }

            // Only possible ways left consume a key, so lets detect we can
            // do all of them
            foreach (var way in possibleWays)
            {
                if (!HasAllKeys(state, way))
                    return null;

                var consumeKeys = way.Requires
                    .Where(x => (x.Flags & EdgeFlags.Consume) != 0)
                    .Select(x => x.Node)
                    .ToArray();
                state = state.UseKeys(consumeKeys);
            }

            // Now visit everything we unlocked
            foreach (var way in possibleWays)
            {
                state = state.Visit(way);
            }

            return state;
        }

        private static bool HasAllKeys(State state, Node node)
        {
            var keys = state.Keys;
            var requiredKeys = node.Requires
                .Where(x => x.Node.Kind == NodeKind.Key)
                .ToArray();
            foreach (var g in requiredKeys.GroupBy(x => x.Node))
            {
                var have = keys.GetCount(g.Key);
                var need = g.Count();
                if (have < need)
                {
                    return false;
                }
            }
            return true;
        }

        private class State
        {
            public Route Route { get; }
            public ImmutableHashSet<Node> Visited { get; }
            public ImmutableHashSet<Node> Next { get; }
            public ImmutableMultiSet<Node> Keys { get; }

            public State(
                Route route,
                ImmutableHashSet<Node> visited,
                ImmutableHashSet<Node> next,
                ImmutableMultiSet<Node> keys)
            {
                Route = route;
                Visited = visited;
                Next = next;
                Keys = keys;
            }

            public State Visit(params Node[] nodes) => Visit((IEnumerable<Node>)nodes);
            public State Visit(IEnumerable<Node> nodes)
            {
                if (!nodes.Any())
                    return this;

                var newNodes = nodes.SelectMany(x => Route.Graph.GetEdges(x));
                var newKeys = nodes
                    .Select(x => Route.GetItemContents(x))
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToArray();
                return new State(
                    Route,
                    Visited.Union(nodes),
                    Next.Except(nodes).Union(newNodes),
                    Keys.AddRange(newKeys));
            }

            public State AddKey(Node key)
            {
                return new State(Route, Visited, Next, Keys.Add(key));
            }

            public State UseKeys(IEnumerable<Node> keys)
            {
                if (!keys.Any())
                    return this;

                return new State(Route, Visited, Next, Keys.RemoveMany(keys));
            }
        }
    }
}
