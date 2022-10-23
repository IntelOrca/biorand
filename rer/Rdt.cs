﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using rer.Opcodes;

namespace rer
{
    internal class Rdt
    {
        public RdtId RdtId { get; }
        public string? OriginalPath { get; set; }
        public string? ModifiedPath { get; set; }
        public string? Script { get; set; }
        public OpcodeBase[] Opcodes { get; set; } = new OpcodeBase[0];

        public IEnumerable<DoorAotSeOpcode> Doors => Opcodes.OfType<DoorAotSeOpcode>();
        public IEnumerable<SceEmSetOpcode> Enemies => Opcodes.OfType<SceEmSetOpcode>();
        public IEnumerable<ItemAotSetOpcode> Items => Opcodes.OfType<ItemAotSetOpcode>();
        public IEnumerable<AotResetOpcode> Resets => Opcodes.OfType<AotResetOpcode>();
        public IEnumerable<SceItemGetOpcode> ItemGets => Opcodes.OfType<SceItemGetOpcode>();
        public IEnumerable<XaOnOpcode> Sounds => Opcodes.OfType<XaOnOpcode>();

        public Rdt(RdtId rdtId)
        {
            RdtId = rdtId;
        }

        public void SetDoorTarget(int id, DoorAotSeOpcode sourceDoor)
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

        public void Nop(int offset)
        {
            foreach (var opcode in Opcodes)
            {
                if (opcode.Offset == offset)
                {
                    if (opcode is UnknownOpcode unk)
                    {
                        unk.NopOut();
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    break;
                }
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModifiedPath!)!);
            File.Copy(OriginalPath!, ModifiedPath!, true);
            using var fs = new FileStream(ModifiedPath!, FileMode.Open, FileAccess.ReadWrite);
            var bw = new BinaryWriter(fs);
            foreach (var opcode in Opcodes)
            {
                fs.Position = opcode.Offset;
                opcode.Write(bw);
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
