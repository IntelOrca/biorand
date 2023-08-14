﻿using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.BioRand
{
    internal static class GameDataReader
    {
        private static void GenerateMapJson(GameData gameData)
        {
            var map = GenerateMap(gameData.Rdts);
            var json = JsonSerializer.Serialize(map, new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText("map.json", json);
        }

        private static Map GenerateMap(IEnumerable<RandomizedRdt> rooms)
        {
            var map = new Map();
            var mapRooms = new Dictionary<string, MapRoom>();
            foreach (var room in rooms)
            {
                var mapRoomDoors = new List<MapRoomDoor>();
                foreach (var door in room.Doors)
                {
                    mapRoomDoors.Add(new MapRoomDoor()
                    {
                        Target = new RdtId(door.NextStage, door.NextRoom).ToString(),
                        Requires = door.LockType == 0 ? new int[0] : new int[] { door.LockType }
                    });
                }
                mapRooms.Add(room.RdtId.ToString(), new MapRoom()
                {
                    Doors = mapRoomDoors.ToArray()
                });
            }
            map.Rooms = mapRooms;
            return map;
        }

        public static RandomizedRdt ReadRdt(BioVersion version, byte[] data, string path, string? modPath)
        {
            var rdtFile = Rdt.FromData(version, data);
            var rdt = Path.GetFileName(path).StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ?
                new RandomizedRdt(rdtFile, RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(4, 3))) :
                new RandomizedRdt(rdtFile, RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(1, 3)));

            rdt.OriginalPath = path;
            rdt.ModifiedPath = modPath;

            rdt.Script = Decompile(rdtFile, false, false);
            rdt.ScriptDisassembly = Decompile(rdtFile, true, false);
            rdt.ScriptListing = Decompile(rdtFile, true, true);

            var opcodeBuilder = new OpcodeBuilder();
            rdtFile.ReadScript(opcodeBuilder);
            rdt.Opcodes = opcodeBuilder.ToArray();

            try
            {
                rdt.Ast = CreateAst(rdtFile);
            }
            catch
            {
            }

            return rdt;
        }

        private static string Decompile(IRdt rdtFile, bool assemblyFormat, bool listingFormat)
        {
            var scriptDecompiler = new ScriptDecompiler(assemblyFormat, listingFormat);
            rdtFile.ReadScript(scriptDecompiler);
            return scriptDecompiler.GetScript();
        }

        private static ScriptAst CreateAst(IRdt rdtFile)
        {
            var builder = new ScriptAstBuilder();
            rdtFile.ReadScript(builder);
            return builder.Ast;
        }
    }
}
