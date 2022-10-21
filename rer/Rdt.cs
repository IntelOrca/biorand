using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace rer
{
    internal class Rdt
    {
        public RdtId RdtId { get; }
        public string? OriginalPath { get; set; }
        public string? ModifiedPath { get; set; }

        public List<Door> Doors = new List<Door>();
        public List<Item> Items = new List<Item>();
        public List<RdtEnemy> Enemies = new List<RdtEnemy>();
        public List<Reset> Resets = new List<Reset>();
        public List<RdtSound> Sounds = new List<RdtSound>();
        public List<NopSequence> NopSequences = new List<NopSequence>();

        public Rdt(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public void AddDoor(Door door)
        {
            Doors.Add(door);
        }

        public void AddItem(Item item)
        {
            Items.Add(item);
        }

        public void AddEnemy(RdtEnemy enemy)
        {
            Enemies.Add(enemy);
        }

        public void AddReset(Reset reset)
        {
            Resets.Add(reset);
        }

        public void AddSound(RdtSound sound)
        {
            Sounds.Add(sound);
        }

        public void SetDoorTarget(int id, Door sourceDoor)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.NextX = sourceDoor.NextX;
                    door.NextY = sourceDoor.NextY;
                    door.NextZ = sourceDoor.NextZ;
                    door.NextD = sourceDoor.NextD;
                    door.Stage = sourceDoor.Stage;
                    door.Room = sourceDoor.Room;
                    door.Camera = sourceDoor.Camera;
                }
            }
        }

        public void SetItem(byte id, ushort type, ushort amount)
        {
            foreach (var item in Items)
            {
                if (item.Id == id)
                {
                    item.Type = type;
                    item.Amount = amount;
                }
            }
            foreach (var reset in Resets)
            {
                if (reset.Id == id && reset.Type != 0)
                {
                    reset.Type = type;
                    reset.Amount = amount;
                }
            }
        }

        public void SetEnemy(byte id, EnemyType type, byte state, byte ai, byte soundBank, byte texture)
        {
            foreach (var enemy in Enemies)
            {
                if (enemy.Id == id)
                {
                    enemy.Type = type;
                    enemy.State = state;
                    enemy.Ai = ai;
                    enemy.SoundBank = soundBank;
                    enemy.Texture = texture;
                }
            }
        }

        public void Nop(int offset, int length)
        {
            NopSequences.Add(new NopSequence(offset, length));
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModifiedPath!)!);
            File.Copy(OriginalPath!, ModifiedPath!, true);
            using var fs = new FileStream(ModifiedPath!, FileMode.Open, FileAccess.ReadWrite);
            var bw = new BinaryWriter(fs);
            foreach (var door in Doors)
            {
                fs.Position = door.Offset;
                door.Write(bw);
            }
            foreach (var item in Items)
            {
                fs.Position = item.Offset;
                item.Write(bw);
            }
            foreach (var enemy in Enemies)
            {
                fs.Position = enemy.Offset;
                enemy.Write(bw);
            }
            foreach (var reset in Resets)
            {
                fs.Position = reset.Offset;
                reset.Write(bw);
            }
            foreach (var nop in NopSequences)
            {
                fs.Position = nop.Offset;
                for (int i = 0; i < nop.Length; i++)
                    bw.Write((byte)0);
            }
        }

        public void Print()
        {
            Console.WriteLine(RdtId);
            Console.WriteLine("------------------------");
            foreach (var door in Doors)
            {
                Console.WriteLine("DOOR  #{0:X2}: {1:X}{2:X2} {3} {4} {5} ({6})",
                    door.Id,
                    door.Stage + 1,
                    door.Room,
                    door.DoorFlag,
                    door.DoorLockFlag,
                    door.DoorKey,
                    door.DoorKey == 0xFF ? "side" : rer.Items.GetItemName(door.DoorKey));
            }
            foreach (var item in Items)
            {
                Console.WriteLine("ITEM  #{0:X2}: {1} x{2}",
                    item.Id,
                    rer.Items.GetItemName(item.Type), item.Amount);
            }
            foreach (var reset in Resets)
            {
                if (Items.Any(x => x.Id == reset.Id))
                {
                    Console.WriteLine("RESET #{0:X2}: {1} x{2}",
                        reset.Id,
                        rer.Items.GetItemName(reset.Type), reset.Amount);
                }
            }
            Console.WriteLine("------------------------");
            Console.WriteLine();
        }

        public override string ToString()
        {
            return RdtId.ToString();
        }
    }

    internal class Door
    {
        public int Offset;
        public byte Opcode;
        public byte Id;
        public ushort Unknown2;
        public ushort Unknown4;
        public short X;
        public short Y;
        public short W;
        public short H;
        public short NextX;
        public short NextY;
        public short NextZ;
        public short NextD;
        public byte Stage;
        public byte Room;
        public byte Camera;
        public byte Unknown19;
        public byte DoorType;
        public byte DoorFlag;
        public byte Unknown1C;
        public byte DoorLockFlag;
        public byte DoorKey;
        public byte Unknown1F;

        public static Door FromReader(BinaryReader br)
        {
            var door = new Door();
            door.Id = br.ReadByte();
            door.Unknown2 = br.ReadUInt16();
            door.Unknown4 = br.ReadUInt16();
            door.X = br.ReadInt16();
            door.Y = br.ReadInt16();
            door.W = br.ReadInt16();
            door.H = br.ReadInt16();
            door.NextX = br.ReadInt16();
            door.NextY = br.ReadInt16();
            door.NextZ = br.ReadInt16();
            door.NextD = br.ReadInt16();
            door.Stage = br.ReadByte();
            door.Room = br.ReadByte();
            door.Camera = br.ReadByte();
            door.Unknown19 = br.ReadByte();
            door.DoorType = br.ReadByte();
            door.DoorFlag = br.ReadByte();
            door.Unknown1C = br.ReadByte();
            door.DoorLockFlag = br.ReadByte();
            door.DoorKey = br.ReadByte();
            door.Unknown1F = br.ReadByte();
            return door;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Id);
            bw.Write(Unknown2);
            bw.Write(Unknown4);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(W);
            bw.Write(H);
            bw.Write(NextX);
            bw.Write(NextY);
            bw.Write(NextZ);
            bw.Write(NextD);
            bw.Write(Stage);
            bw.Write(Room);
            bw.Write(Camera);
            bw.Write(Unknown19);
            bw.Write(DoorType);
            bw.Write(DoorFlag);
            bw.Write(Unknown1C);
            bw.Write(DoorLockFlag);
            bw.Write(DoorKey);
            bw.Write(Unknown1F);
        }
    }

    [DebuggerDisplay("Id = {Id} Type = {Type} Amount = {Amount}")]
    internal class Item
    {
        public int Offset;
        public byte Opcode;
        public byte Id;
        public int Unknown0;
        public short X;
        public short Y;
        public short W;
        public short H;
        public ushort Type;
        public ushort Amount;
        public ushort Array8Idx;
        public ushort Unknown1;

        public void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Id);
            bw.Write(Unknown0);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(W);
            bw.Write(H);
            bw.Write(Type);
            bw.Write(Amount);
            bw.Write(Array8Idx);
            bw.Write(Unknown1);
        }
    }

    internal class Reset
    {
        public int Offset;
        public byte Opcode;
        public byte Id;
        public ushort Unk2;
        public ushort Type;
        public ushort Amount;
        public ushort Unk8;

        public void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Id);
            bw.Write(Unk2);
            bw.Write(Type);
            bw.Write(Amount);
            bw.Write(Unk8);
        }
    }

    [DebuggerDisplay("Id = {Id} Type = {Type} State = {State}")]
    internal class RdtEnemy
    {
        public int Offset;
        public byte Opcode;
        public byte Unk01;
        public byte Id;
        public EnemyType Type;
        public byte State;
        public byte Ai;
        public byte Floor;
        public byte SoundBank;
        public byte Texture;
        public byte KillId;
        public short X;
        public short Y;
        public short Z;
        public short D;
        public ushort Animation;
        public byte Unk15;

        public void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Unk01);
            bw.Write(Id);
            bw.Write((byte)Type);
            bw.Write(State);
            bw.Write(Ai);
            bw.Write(Floor);
            bw.Write(SoundBank);
            bw.Write(Texture);
            bw.Write(KillId);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(Z);
            bw.Write(D);
            bw.Write(Animation);
            bw.Write(Unk15);
        }
    }

    [DebuggerDisplay("Channel = {Channel} Id = {Id}")]
    internal class RdtSound
    {
        public int Offset;
        public byte Opcode;
        public byte Channel;
        public ushort Id;

        public void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Channel);
            bw.Write(Id);
        }
    }

    internal struct NopSequence
    {
        public NopSequence(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; set; }
        public int Length { get; set; }
    }
}
