using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class PoiGraph
    {
        private readonly PointOfInterest[] _poi;

        public PoiGraph(PointOfInterest[] poi)
        {
            _poi = poi;
        }

        public PointOfInterest? GetRandomDoor(Rng rng)
        {
            return GetRandomPoi(rng, x => x.HasTag(PoiKind.Door) || x.HasTag(PoiKind.Stairs));
        }

        public PointOfInterest? GetRandomPoi(Rng rng, Predicate<PointOfInterest> predicate)
        {
            return _poi
                .Where(x => predicate(x))
                .Shuffle(rng)
                .FirstOrDefault();
        }

        public PointOfInterest? FindPoi(int id)
        {
            return _poi.FirstOrDefault(x => x.Id == id);
        }

        public PointOfInterest[] GetTravelRoute(PointOfInterest from, PointOfInterest destination)
        {
            var prev = new Dictionary<PointOfInterest, PointOfInterest>();
            var q = new Queue<PointOfInterest>();
            q.Enqueue(from);

            var found = false;
            while (!found && q.Count != 0)
            {
                var curr = q.Dequeue();
                var edges = GetEdges(curr);
                foreach (var edge in edges)
                {
                    if (!prev.ContainsKey(edge))
                    {
                        prev[edge] = curr;
                        if (edge == destination)
                        {
                            found = true;
                            break;
                        }
                        q.Enqueue(edge);
                    }
                }
            }

            if (!found)
            {
                // throw new Exception("Failed to find POI route from source to destination.");
                return new[] { destination };
            }

            var route = new List<PointOfInterest>();
            var poi = destination;
            while (poi != from)
            {
                route.Add(poi);
                poi = prev[poi];
            }
            return ((IEnumerable<PointOfInterest>)route).Reverse().ToArray();
        }

        public PointOfInterest[] GetEdges(PointOfInterest poi)
        {
            var edges = poi.Edges;
            if (edges == null)
                return new PointOfInterest[0];

            return edges
                .Select(x => FindPoi(x))
                .Where(x => x != null)
                .Select(x => x!)
                .ToArray();
        }

        public PointOfInterest[] GetGraph(PointOfInterest poi)
        {
            var seen = new HashSet<PointOfInterest>();
            var q = new Queue<PointOfInterest>();

            seen.Add(poi);
            q.Enqueue(poi);
            while (q.Count != 0)
            {
                var n = q.Dequeue();
                foreach (var edge in GetEdges(n))
                {
                    if (seen.Add(edge))
                    {
                        q.Enqueue(edge);
                    }
                }
            }
            return seen.ToArray();
        }

        public PointOfInterest[][] GetGraphsContaining(params string[] tags)
        {
            var result = new List<PointOfInterest[]>();
            foreach (var group in GetDisconnectedGraphs())
            {
                var remaining = group.ToList();
                foreach (var tag in tags)
                {
                    var index = remaining.FindIndex(x => x.HasTag(tag));
                    if (index == -1)
                    {
                        goto nextgroup;
                    }
                    remaining.RemoveAt(index);
                }
                result.Add(group);
            nextgroup:
                ;
            }
            return result.ToArray();
        }

        public PointOfInterest[][] GetDisconnectedGraphs()
        {
            var result = new List<PointOfInterest[]>();
            var seen = new HashSet<PointOfInterest>();
            foreach (var poi in _poi)
            {
                if (seen.Contains(poi))
                    continue;

                var group = GetGraph(poi);
                result.Add(group);
                foreach (var d in group)
                {
                    seen.Add(d);
                }
            }
            return result.ToArray();
        }
    }
}
