using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rer
{
    internal static class GameDataReader
    {
        public static GameData Read(string srcGamePath, string rndGamePath)
        {
            var files = Directory.GetFiles(Path.Combine(srcGamePath, @"Pl1\Rdt"));
            var rdts = new List<Rdt>();
            foreach (var file in files)
            {
                var randomFile = Path.Combine(rndGamePath, @"Pl1\Rdt", Path.GetFileName(file));
                try
                {
                    var room = ReadRdt(file, randomFile);
                    rdts.Add(room);
                }
                catch
                {
                }
            }
            return new GameData(rdts.ToArray());
        }

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

        private static Map GenerateMap(IEnumerable<Rdt> rooms)
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
                        Target = new RdtId(door.Stage, door.Room).ToString(),
                        Requires = door.DoorKey == 0 ? new ushort[0] : new ushort[] { door.DoorKey }
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

        private static Rdt ReadRdt(string path, string randomPath)
        {
            var rdtFile = new RdtFile(path);

            var rdt = new Rdt(RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(4, 3)));
            rdt.OriginalPath = path;
            rdt.ModifiedPath = randomPath;

            rdt.Script = Decompile(rdtFile, false);
            rdt.ScriptDisassembly = Decompile(rdtFile, true);

            var opcodeBuilder = new OpcodeBuilder();
            rdtFile.ReadScript(opcodeBuilder);
            rdt.Opcodes = opcodeBuilder.ToArray();

            return rdt;
        }

        private static string Decompile(RdtFile rdtFile, bool assemblyFormat)
        {
            var scriptDecompiler = new ScriptDecompiler(assemblyFormat);
            rdtFile.ReadScript(scriptDecompiler);
            return scriptDecompiler.GetScript();
        }
    }
}
