using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard
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
                        Target = new RdtId(door.NextStage, door.NextRoom).ToString(),
                        Requires = door.LockType == 0 ? new ushort[0] : new ushort[] { door.LockType }
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

        public static Rdt ReadRdt(BioVersion version, string path, string? modPath)
        {
            var rdtFile = new RdtFile(path, version);

            var rdt = new Rdt(rdtFile, RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(4, 3)));
            rdt.OriginalPath = path;
            rdt.ModifiedPath = modPath;

            rdt.Script = Decompile(rdtFile, false);
            rdt.ScriptDisassembly = Decompile(rdtFile, true);

            var opcodeBuilder = new OpcodeBuilder();
            rdtFile.ReadScript(opcodeBuilder);
            rdt.Opcodes = opcodeBuilder.ToArray();

            rdt.Ast = CreateAst(rdtFile);

            return rdt;
        }

        private static string Decompile(RdtFile rdtFile, bool assemblyFormat)
        {
            var scriptDecompiler = new ScriptDecompiler(assemblyFormat);
            rdtFile.ReadScript(scriptDecompiler);
            return scriptDecompiler.GetScript();
        }

        private static ScriptAst CreateAst(RdtFile rdtFile)
        {
            var builder = new ScriptAstBuilder();
            rdtFile.ReadScript(builder);
            return builder.Ast;
        }
    }
}
