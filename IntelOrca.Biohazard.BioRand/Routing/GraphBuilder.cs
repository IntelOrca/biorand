﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Routing
{
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
            var node = new Node(GetNextId(), group, NodeKind.Key, label, new Edge[0]);
            _nodes.Add(node);
            return node;
        }

        public Node Item(int group, string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), group, NodeKind.Item, label, requires.Select(x => new Edge(x)).ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node AndGate(string? label)
        {
            var node = new Node(GetNextId(), 0, NodeKind.AndGate, label, new Edge[0]);
            _nodes.Add(node);
            return node;
        }

        public Node AndGate(string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.AndGate, label, requires.Select(x => new Edge(x)).ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node AndGate(string? label, params Edge[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.AndGate, label, requires.ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node OrGate(string? label, params Node[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.OrGate, label, requires.Select(x => new Edge(x)).ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node OneWay(string? label, params Edge[] requires)
        {
            var node = new Node(GetNextId(), 0, NodeKind.OneWay, label, requires.ToArray());
            _nodes.Add(node);
            return node;
        }

        public Node OneWay(string? label, params Node[] requires) => OneWay(label, requires.Select(x => new Edge(x)).ToArray());

        public Graph Build()
        {
            var edges = new Dictionary<Node, List<Node>>();
            foreach (var c in _nodes)
            {
                foreach (var r in c.Requires.Select(x => x.Node))
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
}
