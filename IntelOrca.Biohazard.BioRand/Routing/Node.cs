using System;

namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Node : IEquatable<Node>
    {
        public int Id { get; }
        public int Group { get; }
        public NodeKind Kind { get; }
        public string? Label { get; }
        public Node[] Requires { get; }

        public Node(int id, int group, NodeKind kind, string? label, Node[] requires)
        {
            Id = id;
            Group = group;
            Kind = kind;
            Label = label;
            Requires = requires;
        }

        public bool IsItem => Kind == NodeKind.Item;
        public bool IsKey =>
            Kind == NodeKind.ReusuableKey ||
            Kind == NodeKind.ConsumableKey ||
            Kind == NodeKind.RemovableKey;

        public bool Equals(Node other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Node node && Equals(node);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"#{Id} ({Label})" ?? $"#{Id}";

        public static bool operator ==(Node a, Node b) => a.Equals(b);
        public static bool operator !=(Node a, Node b) => !a.Equals(b);
    }
}
