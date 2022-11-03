using System;
using System.Collections.Generic;
using System.Linq;

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
        public int[] DoorRandoRouteTokens { get; set; } = Array.Empty<int>();
        public DoorRandoCategory Category { get; set; }

        public PlayNode(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public override string ToString() => RdtId.ToString();
    }

    internal enum LockKind
    {
        None,
        Always,
        Side,
        Gate,
        Unblock
    }

    internal enum DoorRandoCategory
    {
        Include,
        Exclude,
        Static
    }

    internal class PlayEdge
    {
        public RdtId OriginalTargetRdt { get; set; }
        public PlayNode? Node { get; set; }
        public LockKind Lock { get; set; }
        public bool NoReturn { get; set; }
        public ushort[] Requires { get; }
        public PlayNode[] RequiresRoom { get; set; } = Array.Empty<PlayNode>();
        public int? DoorId { get; }
        public DoorEntrance? Entrance { get; set; }
        public bool PreventLoopback { get; set; }
        public bool Randomize { get; set; } = true;

        public PlayEdge(PlayNode node, bool noReturn, ushort[]? requires, int? doorId, DoorEntrance? entrance)
        {
            OriginalTargetRdt = node.RdtId;
            Node = node;
            NoReturn = noReturn;
            Requires = requires ?? new ushort[0];
            DoorId = doorId;
            Entrance = entrance;
        }

        public string RequiresString
        {
            get
            {
                var s = "";
                if (Lock != LockKind.None)
                {
                    s += $"({Lock.ToString().ToLowerInvariant()}) ";
                }
                if (Requires != null && Requires.Length != 0)
                {
                    s += "[" + string.Join(", ", Requires.Select(x => Items.GetItemName(x))) + "] ";
                }
                return s.Trim();
            }
        }

        public override string ToString()
        {
            if (Node == null)
                return "(unconnected)";

            return string.Join(" ", Node.RdtId, RequiresString);
        }
    }
}
