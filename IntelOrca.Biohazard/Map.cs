using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard
{
    public class Map
    {
        public string? StartA { get; set; }
        public string? EndA { get; set; }
        public string? StartB { get; set; }
        public string? EndB { get; set; }
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
        public MapRoomEnemies? Enemies { get; set; }
    }

    public class MapRoomDoor
    {
        public string? Target { get; set; }
        public bool Locked { get; set; }
        public bool NoReturn { get; set; }
        public ushort[]? Requires { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoomItem
    {
        public byte Id { get; set; }
        public int? Type { get; set; }
        public ushort? Amount { get; set; }
        public string? Link { get; set; }
        public string? Priority { get; set; }
        public ushort[]? Requires { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoomEnemies
    {
        public int[]? Nop { get; set; }
        public int[]? ExcludeOffsets { get; set; }
        public int[]? ExcludeIds { get; set; }
        public int[]? ExcludeTypes { get; set; }
        public int[]? IncludeTypes { get; set; }
        public bool KeepState { get; set; }
        public bool KeepAi { get; set; }
        public short? Y { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }
}
