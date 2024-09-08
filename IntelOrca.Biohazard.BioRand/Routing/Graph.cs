﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Routing
{
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
                .Select(x => x.Node)
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
        private static bool RequiresNothingOrKeys(Node node) => node.Requires.All(x => x.Node.Kind == NodeKind.Key);

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
}
