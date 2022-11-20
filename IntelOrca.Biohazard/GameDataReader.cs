using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard
{
    internal static class GameDataReader
    {
        public static Dictionary<RdtId, ulong> GetRdtChecksums(string baseDataPath, int player)
        {
            var result = new Dictionary<RdtId, ulong>();
            var rdtFiles = GetRdtPaths(baseDataPath, player);
            foreach (var path in rdtFiles)
            {
                var rdtId = RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(4, 3));
                var rdtFile = new RdtFile(path);
                result[rdtId] = rdtFile.Checksum;
            }
            return result;
        }

        public static GameData Read(string baseDataPath, string? modDataPath, int player)
        {
            var rdts = new List<Rdt>();
            var rdtFiles = GetRdtPaths(baseDataPath, player);
            foreach (var file in rdtFiles)
            {
                var modRdtPath = modDataPath == null ? null : Path.Combine(modDataPath, @$"Pl{player}\Rdt", Path.GetFileName(file));
                try
                {
                    var rdt = ReadRdt(file, modRdtPath);
                    rdts.Add(rdt);
                }
                catch
                {
                }
            }
            return new GameData(rdts.ToArray());
        }

        private static string[] GetRdtPaths(string baseDataPath, int player)
        {
            var rdtPaths = new List<string>();
            var files = Directory.GetFiles(Path.Combine(baseDataPath, @$"Pl{player}\Rdt"));
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

                rdtPaths.Add(file);
            }
            return rdtPaths.ToArray();
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

        private static Rdt ReadRdt(string path, string? modPath)
        {
            var rdtFile = new RdtFile(path);

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
