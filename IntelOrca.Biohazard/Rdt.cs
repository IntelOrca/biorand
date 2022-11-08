using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class Rdt
    {
        private RdtFile _rdtFile;

        public RdtId RdtId { get; }
        public string? OriginalPath { get; set; }
        public string? ModifiedPath { get; set; }
        public string? Script { get; set; }
        public string? ScriptDisassembly { get; set; }
        public OpcodeBase[] Opcodes { get; set; } = new OpcodeBase[0];
        public ScriptAst? Ast { get; set; }

        public IEnumerable<IDoorAotSetOpcode> Doors => Opcodes.OfType<IDoorAotSetOpcode>();
        public IEnumerable<SceEmSetOpcode> Enemies => Opcodes.OfType<SceEmSetOpcode>();
        public IEnumerable<ItemAotSetOpcode> Items => Opcodes.OfType<ItemAotSetOpcode>();
        public IEnumerable<AotResetOpcode> Resets => Opcodes.OfType<AotResetOpcode>();
        public IEnumerable<SceItemGetOpcode> ItemGets => Opcodes.OfType<SceItemGetOpcode>();
        public IEnumerable<XaOnOpcode> Sounds => Opcodes.OfType<XaOnOpcode>();

        public Rdt(RdtFile rdtFile, RdtId rdtId)
        {
            _rdtFile = rdtFile;
            RdtId = rdtId;
        }

        public IEnumerable<T> EnumerateOpcodes<T>(RandoConfig config) => AstEnumerator<T>.Enumerate(Ast!, config);

        public void SetDoorTarget(int id, RdtId target, DoorEntrance destination, RdtId originalId)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.NextX = destination.X;
                    door.NextY = destination.Y;
                    door.NextZ = destination.Z;
                    door.NextD = destination.D;
                    door.Target = target;
                    door.NextCamera = destination.Camera;
                    door.NextFloor = destination.Floor;
                }
            }
            foreach (var reset in Resets)
            {
                if (reset.Id == id && reset.SCE == 1)
                {
                    reset.Data0 = (ushort)destination.X;
                    reset.Data1 = (ushort)destination.Y;
                    reset.Data2 = (ushort)destination.Z;
                }
            }
            foreach (var cmp in Opcodes.OfType<CmpOpcode>())
            {
                var oldValue = (short)(((originalId.Stage + 1) << 8) | originalId.Room);
                var newValue = (short)(((target.Stage + 1) << 8) | target.Room);
                if (cmp.Flag == 27 && cmp.Value == oldValue)
                {
                    cmp.Value = newValue;
                }
            }
        }

        public void AddDoorLock(int id, byte keyId)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.KeyId = keyId;
                    door.KeyType = 255;
                }
            }
        }

        public void AddDoorUnlock(int id, byte keyId)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.KeyId = keyId;
                    door.KeyType = 254;
                }
            }
        }

        public void RemoveDoorUnlock(int id)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id && door.KeyType == 254)
                {
                    door.KeyId = 0;
                    door.KeyType = 0;
                }
            }
        }

        public void RemoveDoorLock(int id)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.KeyId = 0;
                    door.KeyType = 0;
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
                if (reset.Id == id && reset.SCE == 2 && reset.Data0 != 0)
                {
                    reset.Data0 = type;
                    reset.Data1 = amount;
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
            for (int i = 0; i < Opcodes.Length; i++)
            {
                var opcode = Opcodes[i];
                if (opcode.Offset == offset)
                {
                    if (opcode is UnknownOpcode unk)
                    {
                        unk.NopOut();
                    }
                    else
                    {
                        unk = new UnknownOpcode(offset, new byte[opcode.Length]);
                        Opcodes[i] = unk;
                        unk.NopOut();
                    }
                    break;
                }
            }
        }

        public void Save()
        {
            using (var ms = _rdtFile.GetStream())
            {
                var bw = new BinaryWriter(ms);
                foreach (var opcode in Opcodes)
                {
                    ms.Position = opcode.Offset;
                    opcode.Write(bw);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ModifiedPath!)!);
            File.WriteAllBytes(ModifiedPath!, _rdtFile.Data);
        }

        public void Print()
        {
            Console.WriteLine(RdtId);
            Console.WriteLine("------------------------");
            foreach (var door in Doors)
            {
                Console.WriteLine("DOOR  #{0:X2}: {1:X}{2:X2} {3} {4} {5} ({6})",
                    door.Id,
                    door.NextStage + 1,
                    door.NextRoom,
                    door.Animation,
                    door.KeyId,
                    door.KeyType,
                    door.KeyType == 0xFF ? "side" : IntelOrca.Biohazard.Items.GetItemName(door.KeyType));
            }
            foreach (var item in Items)
            {
                Console.WriteLine("ITEM  #{0:X2}: {1} x{2}",
                    item.Id,
                    IntelOrca.Biohazard.Items.GetItemName(item.Type), item.Amount);
            }
            foreach (var reset in Resets)
            {
                if (Items.Any(x => x.Id == reset.Id))
                {
                    Console.WriteLine("RESET #{0:X2}: {1} x{2}",
                        reset.Id,
                        IntelOrca.Biohazard.Items.GetItemName(reset.Data0), reset.Data1);
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
