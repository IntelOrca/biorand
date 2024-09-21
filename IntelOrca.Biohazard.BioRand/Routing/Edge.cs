namespace IntelOrca.Biohazard.BioRand.Routing
{
    public readonly struct Edge
    {
        public Node Node { get; }
        public EdgeFlags Flags { get; }

        public Edge(Node node) : this(node, 0)
        {
        }

        public Edge(Node node, EdgeFlags flags)
        {
            Node = node;
            Flags = flags;
        }

        public override string ToString() => $"{Node} Flags = {Flags}";
    }
}
