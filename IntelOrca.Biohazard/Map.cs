using System.Collections.Generic;

namespace IntelOrca.Biohazard
{
    public class Map
    {
        public MapStartEnd[]? BeginEndRooms { get; set; }
        public Dictionary<string, MapRoom>? Rooms { get; set; }

        internal MapRoom? GetRoom(RdtId id)
        {
            if (Rooms == null)
                return null;
            Rooms.TryGetValue(id.ToString(), out var value);
            return value;
        }
    }

    public class MapStartEnd
    {
        public string? Start { get; set; }
        public string? End { get; set; }

        public bool? DoorRando { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoom
    {
        public ushort[]? Requires { get; set; }
        public MapRoomDoor[]? Doors { get; set; }
        public MapRoomItem[]? Items { get; set; }
        public MapRoomEnemies[]? Enemies { get; set; }
        public MapRoomNpcs[]? Npcs { get; set; }
        public DoorRandoSpec[]? DoorRando { get; set; }
    }

    public class MapRoomDoor
    {
        public int? Id { get; set; }
        public string? Target { get; set; }
        public bool? Randomize { get; set; }
        public string? Lock { get; set; }
        public bool NoReturn { get; set; }
        public ushort[]? Requires { get; set; }
        public string[]? RequiresRoom { get; set; }
        public bool? DoorRando { get; set; }
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
        public bool? DoorRando { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoomEnemies
    {
        public int[]? Nop { get; set; }
        public int[]? ExcludeOffsets { get; set; }
        public int[]? ExcludeTypes { get; set; }
        public int[]? IncludeTypes { get; set; }
        public bool KeepState { get; set; }
        public bool KeepAi { get; set; }
        public short? Y { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoomNpcs
    {
        public int[]? IncludeOffsets { get; set; }
        public int[]? IncludeTypes { get; set; }
        public int[]? ExcludeTypes { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class DoorRandoSpec
    {
        public string? Category { get; set; }
        public int[]? Nop { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }
}
