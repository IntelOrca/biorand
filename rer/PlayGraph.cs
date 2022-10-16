using System.Collections.Generic;

namespace rer
{
    internal class PlayGraph
    {
        public PlayNode? Start { get; set; }
    }

    internal class PlayNode
    {
        public int Stage { get; set; }
        public int Room { get; set; }
        public byte[]? ItemIds { get; set; }
        public List<PlayEdge> Edges { get; } = new List<PlayEdge>();

        public PlayNode(int stage, int room)
        {
            Stage = stage;
            Room = room;
        }

        public override string ToString() => Utility.GetHumanRoomId(Stage, Room);
    }

    internal class PlayEdge
    {
        public PlayNode Node { get; }
        public bool Locked { get; set; }
        public ushort[] Requires { get; }

        public PlayEdge(PlayNode node, bool locked, ushort[] requires)
        {
            Node = node;
            Locked = locked;
            Requires = requires;
        }

        public override string ToString()
        {
            var s = Utility.GetHumanRoomId(Node.Stage, Node.Room);
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
