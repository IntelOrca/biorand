using System.Collections.Generic;

namespace rer
{
    internal class PlayGraph
    {
        public PlayNode? Start { get; set; }
        public PlayNode? End { get; set; }
    }

    internal class PlayNode
    {
        public RdtId RdtId { get; set; }
        public ItemPoolEntry[]? Items { get; set; }
        public List<PlayEdge> Edges { get; } = new List<PlayEdge>();

        public PlayNode(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public override string ToString() => RdtId.ToString();
    }

    internal class PlayEdge
    {
        public PlayNode Node { get; }
        public bool Locked { get; set; }
        public bool NoReturn { get; set; }
        public ushort[] Requires { get; }

        public PlayEdge(PlayNode node, bool locked, bool noReturn, ushort[] requires)
        {
            Node = node;
            Locked = locked;
            NoReturn = noReturn;
            Requires = requires;
        }

        public override string ToString()
        {
            var s = Node.RdtId.ToString();
            if (Locked)
            {
                s += " (locked)";
            }
            if (Requires.Length != 0)
            {
                s += " [" + string.Join(", ", Requires) + "]";
            }
            return s;
        }
    }
}
