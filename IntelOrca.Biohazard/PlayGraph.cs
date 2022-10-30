using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard
{
    internal class PlayGraph
    {
        public PlayNode? Start { get; set; }
        public PlayNode? End { get; set; }
    }

    internal class PlayNode
    {
        public RdtId RdtId { get; set; }
        public ItemPoolEntry[] Items { get; set; } = Array.Empty<ItemPoolEntry>();
        public Dictionary<byte, RdtItemId> LinkedItems { get; set; } = new Dictionary<byte, RdtItemId>();
        public ushort[] Requires { get; set; } = Array.Empty<ushort>();
        public List<PlayEdge> Edges { get; } = new List<PlayEdge>();
        public PlayNodeDoor[] Doors { get; set; } = Array.Empty<PlayNodeDoor>();

        public PlayNode(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public override string ToString() => RdtId.ToString();
    }

    internal class PlayEdge
    {
        public RdtId OriginalTargetRdt { get; set; }
        public PlayNode? Node { get; set; }
        public bool Locked { get; set; }
        public bool NoReturn { get; set; }
        public ushort[] Requires { get; }

        public PlayEdge(PlayNode node, bool locked, bool noReturn, ushort[] requires)
        {
            OriginalTargetRdt = node.RdtId;
            Node = node;
            Locked = locked;
            NoReturn = noReturn;
            Requires = requires;
        }

        public override string ToString()
        {
            if (Node == null)
                return "(unconnected)";

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
