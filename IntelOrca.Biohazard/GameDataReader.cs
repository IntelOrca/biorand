using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard
{
    internal static class GameDataReader
    {
        public static GameData Read(string srcGamePath, string rndGamePath, int player)
        {
            var files = Directory.GetFiles(Path.Combine(srcGamePath, @$"Pl{player}\Rdt"));
            var rdts = new List<Rdt>();
            foreach (var file in files)
            {
                // Check the file is an RDT file
                var fileName = Path.GetFileName(file);
                if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                    !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ignore RDTs that are not part of the main game
                if (!char.IsDigit(fileName[4]))
                    continue;

                var randomFile = Path.Combine(rndGamePath, @$"Pl{player}\Rdt", Path.GetFileName(file));
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
                        Target = new RdtId(door.NextStage, door.NextRoom).ToString(),
                        Requires = door.KeyType == 0 ? new ushort[0] : new ushort[] { door.KeyType }
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

            var rdt = new Rdt(rdtFile, RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(4, 3)));
            rdt.OriginalPath = path;
            rdt.ModifiedPath = randomPath;

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
