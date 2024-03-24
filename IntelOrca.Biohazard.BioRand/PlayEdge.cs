using System.Linq;

namespace IntelOrca.Biohazard.BioRand
{
    internal class PlayEdge
    {
        public PlayNode Parent { get; }
        public RdtId OriginalTargetRdt { get; set; }
        public PlayNode? Node { get; set; }
        public LockKind Lock { get; set; }
        public byte LockId { get; set; }
        public bool NoReturn { get; set; }
        public PlayRequirement[] Requires { get; set; }
        public PlayNode[] RequiresRoom { get; set; }
        public int? DoorId { get; set; }
        public DoorEntrance? Entrance { get; set; }
        public bool Randomize { get; set; } = true;
        public MapRoomDoor Raw { get; }

        public PlayEdge(PlayNode parent, MapRoomDoor raw)
        {
            Parent = parent;
            Raw = raw;
        }

        public string GetRequiresString(IItemHelper? itemHelper)
        {
            var s = "";
            if (Lock == LockKind.Side)
            {
                s += $"(locked #{LockId}) ";
            }
            else if (Lock != LockKind.None)
            {
                s += $"({Lock.ToString().ToLowerInvariant()}) ";
            }
            if (Requires != null && Requires.Length != 0)
            {
                s += "[" + string.Join(", ", Requires.Select(x => x.ToString(itemHelper))) + "] ";
            }
            return s.Trim();
        }

        private string GetItemName(IItemHelper? itemHelper, byte item)
        {
            if (itemHelper == null)
                return item.ToString();
            return itemHelper.GetItemName(item);
        }

        public byte[] KeyRequires => Requires.Where(x => x.Kind == PlayRequirementKind.Key).Select(x => (byte)x.Id).ToArray();

        public override string ToString()
        {
            if (Node == null)
                return $"{Parent} -> (unconnected)";

            return $"{Parent} -> {Node.RdtId} {GetRequiresString(null)}";
        }
    }
}
