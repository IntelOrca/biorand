using System.Collections.Generic;
using System.Linq;

namespace rer
{
    public class Map
    {
        public string? Start { get; set; }
        public string? End { get; set; }
        public Dictionary<string, MapRoom>? Rooms { get; set; }

        internal MapRoom? GetRoom(RdtId id)
        {
            if (Rooms == null)
                return null;
            Rooms.TryGetValue(id.ToString(), out var value);
            return value;
        }
    }

    public class MapStart
    {
        public int Stage { get; set; }
        public int Room { get; set; }
    }

    public class MapRoom
    {
        public int[]? SupportedNpcs { get; set; }
        public ushort[]? Requires { get; set; }
        public MapRoomDoor[]? Doors { get; set; }
        public MapRoomItem[]? Items { get; set; }
    }

    public class MapRoomDoor
    {
        public string? Target { get; set; }
        public bool Locked { get; set; }
        public bool NoReturn { get; set; }

        private ushort[]? _requires;
        public ushort[]? Requires
        {
            get
            {
                if (_requires == null || _requires.Length == 0)
                    return null;
                return _requires;
            }
            set => _requires = value;
        }
    }

    public class MapRoomItem
    {
        public byte Id { get; set; }
        public int Type { get; set; }
        public ushort? Amount { get; set; }
        public string? Link { get; set; }
        public string? Priority { get; set; }
        public ushort[]? Requires { get; set; }
    }
}
