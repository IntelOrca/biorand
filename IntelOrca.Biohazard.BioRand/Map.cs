﻿using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace IntelOrca.Biohazard.BioRand
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

        internal static int[] ParseNopArray(System.Text.Json.JsonElement[]? nopArray, RandomizedRdt rdt)
        {
            var nop = new List<int>();
            if (nopArray != null)
            {
                foreach (var entry in nopArray)
                {
                    if (entry.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = entry.GetString()!;
                        if (s.Contains('-'))
                        {
                            var parts = s.Split('-');
                            var lower = ParseLiteral(parts[0]);
                            var upper = ParseLiteral(parts[1]);
                            foreach (var op in rdt.Opcodes)
                            {
                                if (op.Offset >= lower && op.Offset <= upper)
                                {
                                    nop.Add(op.Offset);
                                }
                            }
                        }
                        else
                        {
                            nop.Add(ParseLiteral(s));
                        }
                    }
                    else
                    {
                        nop.Add(entry.GetInt32());
                    }
                }
            }
            return nop.ToArray();
        }

        private static int ParseLiteral(string s)
        {
            if (s.StartsWith("0x"))
            {
                return int.Parse(s.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            return int.Parse(s);
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
        public string? LinkedRoom { get; set; }
        public int[]? Requires { get; set; }
        public MapRoomDoor[]? Doors { get; set; }
        public MapRoomItem[]? Items { get; set; }
        public MapRoomEnemies[]? Enemies { get; set; }
        public MapRoomNpcs[]? Npcs { get; set; }
        public DoorRandoSpec[]? DoorRando { get; set; }
    }

    public class MapRoomDoor
    {
        public string? Condition { get; set; }
        public bool Create { get; set; }
        public int Texture { get; set; }
        public int? Special { get; set; }
        public int? Id { get; set; }
        public int? AltId { get; set; }
        public JsonElement[]? Offsets { get; set; }
        public byte? Cut { get; set; }
        public MapRoomDoorEntrance? Entrance { get; set; }
        public int? EntranceId { get; set; }
        public string? Target { get; set; }
        public bool? Randomize { get; set; }
        public string? Lock { get; set; }
        public byte? LockId { get; set; }
        public bool NoReturn { get; set; }
        public bool NoUnlock { get; set; }
        public bool IsBridgeEdge { get; set; }
        public int[]? Requires { get; set; }
        public string[]? RequiresRoom { get; set; }
        public bool? DoorRando { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoomDoorEntrance
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public int Floor { get; set; }
        public int Cut { get; set; }
    }

    public class MapRoomItem
    {
        public System.Text.Json.JsonElement[]? Nop { get; set; }
        public JsonElement[]? Offsets { get; set; }
        public byte Id { get; set; }
        public byte? ItemId { get; set; }
        public short? GlobalId { get; set; }
        public byte? Type { get; set; }
        public byte? Amount { get; set; }
        public string? Link { get; set; }
        public string? Priority { get; set; }
        public int[]? Requires { get; set; }
        public string[]? RequiresRoom { get; set; }
        public bool? AllowDocuments { get; set; }
        public bool? DoorRando { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }

    public class MapRoomEnemies
    {
        public System.Text.Json.JsonElement[]? Nop { get; set; }
        public int[]? ExcludeOffsets { get; set; }
        public int[]? ExcludeTypes { get; set; }
        public int[]? IncludeTypes { get; set; }
        public bool KeepState { get; set; }
        public bool KeepAi { get; set; }
        public bool KeepPositions { get; set; }
        public bool IgnoreRatio { get; set; }
        public short? Y { get; set; }
        public int? MaxDifficulty { get; set; }
        public bool? Restricted { get; set; }
        public string? Condition { get; set; }

        // Filters
        public bool? RandomPlacements { get; set; }
        public bool? DoorRando { get; set; }
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
        public bool? DoorRando { get; set; }
        public int Cutscene { get; set; }
        public string? PlayerActor { get; set; }
        public bool? EmrScale { get; set; }
        public string? Use { get; set; }
    }

    public class DoorRandoSpec
    {
        public string? Category { get; set; }
        public System.Text.Json.JsonElement[]? Nop { get; set; }
        public bool Cutscene { get; set; }
        public int? Player { get; set; }
        public int? Scenario { get; set; }
    }
}
