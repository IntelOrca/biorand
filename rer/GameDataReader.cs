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
            var room = new Rdt(RdtId.Parse(Path.GetFileNameWithoutExtension(path).Substring(4, 3)));
            room.OriginalPath = path;
            room.ModifiedPath = randomPath;

            // if (room.RdtId == RdtId.Parse("202"))
            //     room.OriginalPath = path;
            // else
            //     return room;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fs);
                br.ReadByte();
                br.ReadByte();
                br.ReadByte();
                br.ReadByte();
                br.ReadByte();
                br.ReadByte();
                br.ReadByte();
                br.ReadByte();

                var offsets = new (int, int)[23];
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = (i, br.ReadInt32());
                }

                var scriptOffset = offsets[16].Item2;
                fs.Position = scriptOffset;
                var len = offsets[17].Item2 - scriptOffset;
                var s1 = ReadScript(room, br, len, "init");
                // Console.WriteLine(s1);
                // Console.WriteLine();

                scriptOffset = offsets[17].Item2;
                fs.Position = scriptOffset;
                len = offsets[18].Item2 - scriptOffset;
                var s2 = ReadScript(room, br, len, "main");
                // Console.WriteLine(s2);
            }
            catch (Exception ex)
            {
                // Console.WriteLine(ex.Message);
            }
            return room;
        }

        private static string ReadScript(Rdt room, BinaryReader br, int length, string name)
        {
            var fs = br.BaseStream;

            var sb = new ScriptBuilder();
            sb.WriteLine(name + ":");
            sb.Indent();

            var start = (int)fs.Position;
            var functionOffsets = new List<int>();
            var firstFunctionOffset = br.ReadUInt16();
            functionOffsets.Add(start + firstFunctionOffset);
            var numFunctions = firstFunctionOffset / 2;
            for (int i = 1; i < numFunctions; i++)
            {
                functionOffsets.Add(start + br.ReadUInt16());
            }
            functionOffsets.Add(start + length);
            for (int i = 0; i < numFunctions; i++)
            {
                if (i != 0)
                {
                    sb.ResetIndent();
                    sb.WriteLine();
                    sb.WriteLine($"sub_{i:X2}:");
                    sb.Indent();
                }

                var expectingEndIf = false;
                var functionOffset = functionOffsets[i];
                var functionEnd = functionOffsets[i + 1];
                fs.Position = functionOffset;
                while (fs.Position < functionEnd)
                {
                    var instructionPosition = (int)fs.Position;
                    var opcode = (Opcode)br.ReadByte();
                    if (i == numFunctions - 1 && (byte)opcode >= _instructionSizes.Length)
                    {
                        break;
                    }
                    var nextPosition = instructionPosition + _instructionSizes[(byte)opcode];
                    switch (opcode)
                    {
                        default:
                            if (EnemyType.IsDefined(opcode))
                            {
                                sb.WriteLine($"{opcode}();");
                            }
                            else
                            {
                                sb.WriteLine($"op_{opcode:X}();");
                            }
                            break;
                        case Opcode.Nop:
                            break;
                        case Opcode.EvtEnd:
                            {
                                var ret = br.ReadByte();
                                sb.WriteLine($"return {ret};");
                                break;
                            }
                        case Opcode.IfelCk:
                            sb.Write($"if (");
                            sb.Indent();
                            break;
                        case Opcode.ElseCk:
                            if (expectingEndIf)
                            {
                                sb.Unindent();
                                sb.WriteLine($"end-if");
                            }
                            sb.Unindent();
                            sb.WriteLine($"else");
                            sb.Indent();
                            expectingEndIf = true;
                            break;
                        case Opcode.EndIf:
                            sb.Unindent();
                            sb.WriteLine($"endif");
                            expectingEndIf = false;
                            break;
                        case Opcode.Sleep:
                            {
                                br.ReadByte();
                                var count = br.ReadUInt16();
                                sb.WriteLine($"sleep({count});");
                                break;
                            }
                        case Opcode.Sleeping:
                            {
                                var count = br.ReadUInt16();
                                sb.WriteLine($"sleeping({count});");
                                break;
                            }
                        case Opcode.Wsleep:
                            {
                                sb.WriteLine($"wsleep();");
                                break;
                            }
                        case Opcode.Wsleeping:
                            {
                                sb.WriteLine($"wsleeping();");
                                break;
                            }
                        case Opcode.For:
                            {
                                br.ReadByte();
                                var blockLen = br.ReadUInt16();
                                var count = br.ReadUInt16();
                                sb.WriteLine($"for {count} times");
                                sb.Indent();
                                break;
                            }
                        case Opcode.Next:
                            {
                                sb.Unindent();
                                sb.WriteLine($"next");
                                break;
                            }
                        case Opcode.While:
                            {
                                sb.WriteLine($"while (");
                                sb.Indent();
                                break;
                            }
                        case Opcode.Ewhile:
                            {
                                sb.Unindent();
                                sb.WriteLine($"next");
                                break;
                            }
                        case Opcode.Do:
                            {
                                sb.WriteLine($"do");
                                sb.Indent();
                                break;
                            }
                        case Opcode.Edwhile:
                            {
                                sb.Unindent();
                                sb.WriteLine($"while (");
                                break;
                            }
                        case Opcode.Switch:
                            {
                                var varw = br.ReadByte();
                                sb.WriteLine($"switch (var_{varw:X2})");
                                sb.Indent();
                                break;
                            }
                        case Opcode.Case:
                            {
                                br.ReadByte();
                                br.ReadUInt16();
                                var value = br.ReadUInt16();
                                sb.Unindent();
                                sb.WriteLine($"case {value}:");
                                sb.Indent();
                                break;
                            }
                        case Opcode.Default:
                            {
                                sb.Unindent();
                                sb.WriteLine($"default:");
                                sb.Indent();
                                break;
                            }
                        case Opcode.Eswitch:
                            {
                                sb.Unindent();
                                sb.WriteLine($"end-switch");
                                break;
                            }
                        case Opcode.Gosub:
                            {
                                var num = br.ReadByte();
                                sb.WriteLine($"sub_{num:X2}();");
                                break;
                            }
                        case Opcode.Return:
                            {
                                sb.WriteLine($"return;");
                                break;
                            }
                        case Opcode.Break:
                            {
                                sb.WriteLine("break;");
                                break;
                            }
                        case Opcode.Ck:
                            {
                                var bitArray = br.ReadByte();
                                var number = br.ReadByte();
                                var value = br.ReadByte();
                                if (value == 0)
                                    sb.Write($"!");
                                sb.WriteLine($"bits[{bitArray}][{number}])");
                                break;
                            }
                        case Opcode.ObjModelSet:
                            {
                                var id = br.ReadByte();
                                sb.WriteLine($"obj_model_set({id}, ...)");
                                break;
                            }
                        case Opcode.WorkSet:
                            {
                                var kind = br.ReadByte();
                                var id = br.ReadByte();

                                var kindS = kind.ToString();
                                if (kind == 1)
                                    kindS = "wk_player";
                                else if (kind == 3)
                                    kindS = "wk_entity";
                                else if (kind == 4)
                                    kindS = "wk_door";

                                sb.WriteLine($"work_set({kindS}, {id});");
                                break;
                            }
                        case Opcode.Set:
                            {
                                var bitArray = br.ReadByte();
                                var number = br.ReadByte();
                                var opChg = br.ReadByte();
                                sb.Write($"bits[{bitArray}][{number}]");
                                if (opChg == 0)
                                    sb.WriteLine(" = 0;");
                                else if (opChg == 1)
                                    sb.WriteLine(" = 1;");
                                else if (opChg == 7)
                                    sb.WriteLine(" ^= 1;");
                                else
                                    sb.WriteLine(" (INVALID);");
                                break;
                            }
                        case Opcode.AotSet:
                            {
                                var id = br.ReadByte();
                                var type = br.ReadByte();
                                br.ReadBytes(3);
                                br.ReadBytes(8);
                                br.ReadBytes(6);
                                sb.WriteLine($"aot_set({id}, 0x{type:X});");
                                break;
                            }
                        case Opcode.PosSet:
                            {
                                var x = br.ReadInt16();
                                var y = br.ReadInt16();
                                var z = br.ReadInt16();
                                sb.WriteLine($"pos_set({x}, {y}, {z});");
                                break;
                            }
                        case Opcode.DoorAotSe:
                            {
                                var door = new Door()
                                {
                                    Offset = instructionPosition,
                                    Opcode = (byte)opcode,
                                    Id = br.ReadByte(),
                                    Unknown2 = br.ReadUInt16(),
                                    Unknown4 = br.ReadUInt16(),
                                    X = br.ReadInt16(),
                                    Y = br.ReadInt16(),
                                    W = br.ReadInt16(),
                                    H = br.ReadInt16(),
                                    NextX = br.ReadInt16(),
                                    NextY = br.ReadInt16(),
                                    NextZ = br.ReadInt16(),
                                    NextD = br.ReadInt16(),
                                    Stage = br.ReadByte(),
                                    Room = br.ReadByte(),
                                    Camera = br.ReadByte(),
                                    Unknown19 = br.ReadByte(),
                                    DoorType = br.ReadByte(),
                                    DoorFlag = br.ReadByte(),
                                    Unknown1C = br.ReadByte(),
                                    DoorLockFlag = br.ReadByte(),
                                    DoorKey = br.ReadByte(),
                                    Unknown1F = br.ReadByte()
                                };
                                room.AddDoor(door);
                                sb.WriteLine($"door_aot_se({door.Id}, 0x{door.Stage:X}, 0x{door.Room:X2}, {door.DoorFlag}, {door.DoorLockFlag}, {door.DoorKey});");
                                break;
                            }
                        case Opcode.SceEmSet:
                            {
                                var enemy = new RdtEnemy()
                                {
                                    Offset = instructionPosition,
                                    Opcode = (byte)opcode,
                                    Unk01 = br.ReadByte(),
                                    Id = br.ReadByte(),
                                    Type = (EnemyType)br.ReadByte(),
                                    State = br.ReadByte(),
                                    Ai = br.ReadByte(),
                                    Floor = br.ReadByte(),
                                    SoundBank = br.ReadByte(),
                                    Texture = br.ReadByte(),
                                    KillId = br.ReadByte(),
                                    X = br.ReadInt16(),
                                    Y = br.ReadInt16(),
                                    Z = br.ReadInt16(),
                                    D = br.ReadInt16(),
                                    Animation = br.ReadUInt16(),
                                    Unk15 = br.ReadByte(),
                                };
                                room.AddEnemy(enemy);
                                sb.WriteLine($"sce_em_set({enemy.Id}, {enemy.Type}, {enemy.State}, {enemy.Ai}, {enemy.Floor}, {enemy.SoundBank}, {enemy.Texture}, {enemy.KillId}, ..., {enemy.Animation});");
                                break;
                            }
                        case Opcode.AotReset:
                            {
                                var reset = new Reset()
                                {
                                    Offset = instructionPosition,
                                    Opcode = (byte)opcode,
                                    Id = br.ReadByte(),
                                    Unk2 = br.ReadUInt16(),
                                    Type = br.ReadUInt16(),
                                    Amount = br.ReadUInt16(),
                                    Unk8 = br.ReadUInt16(),
                                };
                                room.AddReset(reset);
                                sb.WriteLine($"aot_reset({reset.Id}, 0x{reset.Type:X2}, 0x{reset.Amount:X2}, 0x{reset.Unk8:X2});");
                                break;
                            }
                        case Opcode.AotOn:
                            {
                                var id = br.ReadByte();
                                sb.WriteLine($"aot_on({id});");
                                break;
                            }
                        case Opcode.ItemAotSet:
                            {
                                var item = new Item()
                                {
                                    Offset = instructionPosition,
                                    Opcode = (byte)opcode,
                                    Id = br.ReadByte(),
                                    Unknown0 = br.ReadInt32(),
                                    X = br.ReadInt16(),
                                    Y = br.ReadInt16(),
                                    W = br.ReadInt16(),
                                    H = br.ReadInt16(),
                                    Type = br.ReadUInt16(),
                                    Amount = br.ReadUInt16(),
                                    Array8Idx = br.ReadUInt16(),
                                    Unknown1 = br.ReadUInt16(),
                                };
                                room.AddItem(item);
                                sb.WriteLine($"item_aot_set({item.Id}, 0x{item.Type:X2}, 0x{item.Amount:X2});");
                                break;
                            }
                        case Opcode.SceBgmControl:
                            {
                                var bgm = br.ReadByte();
                                var action = br.ReadByte();
                                var dummy = br.ReadByte();
                                var volume = br.ReadByte();
                                var channel = br.ReadByte();
                                sb.WriteLine($"sce_bgm_control({bgm},{action},{volume},{channel});");
                                break;
                            }
                        case Opcode.SceBgmtblSet:
                            {
                                var dummy = br.ReadByte();
                                var roomId = br.ReadByte();
                                var stage = br.ReadByte();
                                var main = br.ReadByte();
                                var sub = br.ReadByte();
                                var dummy1 = br.ReadByte();
                                var dummy2 = br.ReadByte();
                                sb.WriteLine($"sce_bgmtbl_set({stage:X}{roomId:X2} = MAIN{main:X2} SUB{sub:X2});");
                                break;
                            }
                        case Opcode.XaOn:
                            {
                                var channel = br.ReadByte();
                                var id = br.ReadInt16();
                                sb.WriteLine($"xa_on({channel}, {id});");
                                break;
                            }
                        case Opcode.SceItemLost:
                            {
                                var item = br.ReadByte();
                                sb.WriteLine($"sce_item_lost(0x{item:X});");
                                break;
                            }
                        case Opcode.DoorAotSet4p:
                            {
                                var id = br.ReadByte();
                                sb.WriteLine($"door_aot_set_4p({id});");
                                break;
                            }
                        case Opcode.ItemAotSet4p:
                            {
                                var id = br.ReadByte();
                                sb.WriteLine($"item_aot_set_4p({id});");
                                break;
                            }
                        case Opcode.SceItemGet:
                            {
                                var type = br.ReadByte();
                                var amount = br.ReadByte();
                                sb.WriteLine($"sce_item_get(0x{type}, {amount});");
                                break;
                            }
                        case Opcode.Unk81:
                            throw new Exception();
                    }
                    fs.Position = nextPosition;
                }
            }

            return sb.ToString();
        }

        private static int[] _instructionSizes = new int[]
        {
            1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4,
            2, 4, 2, 4, 6, 2, 2, 6, 2, 2, 2, 6, 1, 4, 1, 1,
            1, 4, 4, 6, 4, 3, 6, 4, 1, 2, 1, 6, 20, 38, 3, 4,
            1, 1, 8, 8, 4, 3, 12, 4, 3, 8, 16, 32, 2, 3, 6, 4,
            8, 10, 1, 4, 22, 5, 10, 2, 16, 8, 2, 3, 5, 22, 22, 4,
            4, 6, 6, 6, 22, 6, 4, 8, 4, 4, 2, 2, 3, 2, 2, 2,
            14, 4, 2, 1, 16, 2, 1, 28, 40, 30, 6, 4, 1, 4, 6, 2,
            1, 1, 16, 8, 4, 22, 3, 4, 6, 1, 16, 16, 6, 6, 6, 6,
            2, 0, 3, 1, 2, 6, 1, 1, 3, 1, 6, 6, 8, 24, 24
        };
    }
}
