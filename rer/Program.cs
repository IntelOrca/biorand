using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using rer;

class Program
{
    private static Random _random = new Random();

    public static void Main(string[] args)
    {
        var factory = new PlayGraphFactory();
        var gameData = ReadGameData();
        factory.Create(gameData, @"M:\git\rer\rer\map.json");
    }

    private static GameData ReadGameData()
    {
        // RandomiseBgm();

        var files = Directory.GetFiles(@"F:\games\re2\data\Pl1\Rdt");
        var rdts = new List<Rdt>();
        foreach (var file in files)
        {
            var randomFile = Path.Combine(@"F:\games\re2r\data\Pl1\Rdt", Path.GetFileName(file));
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

        // var map = GenerateMap(rooms);
        // var json = JsonSerializer.Serialize(map, new JsonSerializerOptions()
        // {
        //     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        //     WriteIndented = true
        // });
        // File.WriteAllText("map.json", json);


        // var firstRoom = rooms[0];
        // var secondRoom = rooms[1];
        // firstRoom.SetDoorTarget(firstRoom.Doors[0].Id, secondRoom.Doors[1]);
        // firstRoom.Save();
    }

    private static Map GenerateMap(IEnumerable<Rdt> rooms)
    {
        var map = new Map();
        var mapRooms = new List<MapRoom>();
        foreach (var room in rooms)
        {
            var mapRoomDoors = new List<MapRoomDoor>();
            foreach (var door in room.Doors)
            {
                mapRoomDoors.Add(new MapRoomDoor()
                {
                    Stage = door.Stage,
                    Room = door.Room,
                    Requires = door.DoorKey == 0 ? new ushort[0] : new ushort[] { door.DoorKey }
                });
            }
            mapRooms.Add(new MapRoom()
            {
                Stage = room.Stage,
                Room = room.RoomId,
                Doors = mapRoomDoors.ToArray()
            });
        }
        map.Rooms = mapRooms.ToArray();
        return map;
    }

    private static void RandomiseBgm()
    {
        var list = new[]
        {
            "MAIN01",
            "MAIN02",
            "MAIN03",
            "MAIN05",
            "MAIN07",
            "MAIN08",
            "MAIN0A",
            "MAIN0D",
            "MAIN0F",
            "MAIN10",
            "MAIN15",
            "MAIN16",
            "MAIN21",
            "MAIN29",
            "MAIN31",
            "MAIN0E",
            "MAIN13",
            "MAIN20",
            "SUB00",
            "SUB01",
            "SUB04",
            "SUB09",
            "SUB0B",
            "SUB0C",
            "SUB0E",
            "SUB0F",
            "SUB10",
            "SUB11",
            "SUB12",
            "SUB13",
            "SUB14",
            "SUB15",
            "SUB16",
            "SUB17",
            "SUB18",
            "SUB0A",
        };
        var destList = (string[])list.Clone();
        for (int i = 0; i < destList.Length; i++)
        {
            var ri = _random.Next(0, destList.Length);
            var tmp = destList[i];
            destList[i] = destList[ri];
            destList[ri] = tmp;
        }

        var srcDir = @"F:\games\re2\data\Common\Sound\BGM";
        var dstDir = @"F:\games\re2r\data\Common\Sound\BGM";

        for (int i = 0; i < list.Length; i++)
        {
            var src = Path.Combine(srcDir, FixSubFilename(list[i]) + ".bgm");
            var dst = Path.Combine(dstDir, FixSubFilename(destList[i]) + ".bgm");
            File.Copy(src, dst, true);
            src = Path.Combine(srcDir, list[i] + ".sap");
            dst = Path.Combine(dstDir, destList[i] + ".sap");
            File.Copy(src, dst, true);
        }
    }

    private static string FixSubFilename(string s)
    {
        if (s.StartsWith("SUB"))
            return "SUB_" + s.Substring(3);
        return s;
    }

    private static Rdt ReadRdt(string path, string randomPath)
    {
        File.Copy(path, randomPath, true);

        var room = new Rdt();
        room.Id = Path.GetFileNameWithoutExtension(path).Substring(4, 3);
        room.Stage = int.Parse(room.Id.Substring(0, 1), NumberStyles.HexNumber) - 1;
        room.RoomId = int.Parse(room.Id.Substring(1, 2), NumberStyles.HexNumber);
        room.OriginalPath = path;
        room.ModifiedPath = randomPath;

        // if (room.Id == "200")
        //     room.Id = room.Id;
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
            Console.WriteLine(ex.Message);
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
                        if (!Enum.IsDefined<Opcode>(opcode))
                        {
                            opcode = opcode;
                        }

                        sb.WriteLine($"{opcode}();");
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
                            sb.WriteLine($"endif");
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
                    case Opcode.Gosub:
                        {
                            var num = br.ReadByte();
                            sb.WriteLine($"sub_{num:X2}();");
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
                            else if(kind == 4)
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
                            break;
                        }
                    case Opcode.SceBgmControl:
                        {
                            var bgm = br.ReadByte();
                            var action = br.ReadByte();
                            var dummy = br.ReadByte();
                            var volume = br.ReadByte();
                            var channel = br.ReadByte();
                            // Console.WriteLine($"{bgm},{action},{volume},{channel}");
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
                            // Console.WriteLine($"{stage:X}{roomId:X2} = MAIN{main:X2} SUB{sub:X2}");
                            break;
                        }
                    case Opcode.DoorAotSet4p:
                        fs.Position += 39;
                        break;
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
