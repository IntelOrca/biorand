using System.Linq;

namespace rer
{
    public class Map
    {
        public MapStart? Start { get; set; }
        public MapStart? End { get; set; }
        public MapRoom[]? Rooms { get; set; }

        public MapRoom? GetRoom(int stage, int room)
        {
            return Rooms?.FirstOrDefault(x => x.Stage == stage && x.Room == room);
        }
    }

    public class MapStart
    {
        public int Stage { get; set; }
        public int Room { get; set; }
    }

    public class MapRoom
    {
        public int Stage { get; set; }
        public int Room { get; set; }
        public MapRoomDoor[]? Doors { get; set; }
        public MapRoomItem[]? Items { get; set; }
    }

    public class MapRoomDoor
    {
        public int Stage { get; set; }
        public int Room { get; set; }
        public bool Locked { get; set; }
        public bool NoReturn { get; set; }
        public ushort[]? Requires { get; set; }
    }

    public class MapRoomItem
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public ushort[]? Requires { get; set; }
    }
}
