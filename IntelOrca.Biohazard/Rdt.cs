using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class Rdt
    {
        private RdtFile _rdtFile;

        public BioVersion Version => _rdtFile.Version;
        public RdtId RdtId { get; }
        public string? OriginalPath { get; set; }
        public string? ModifiedPath { get; set; }
        public string? Script { get; set; }
        public string? ScriptDisassembly { get; set; }
        public string? ScriptListing { get; set; }
        public OpcodeBase[] Opcodes { get; set; } = new OpcodeBase[0];
        public ScriptAst? Ast { get; set; }
        public List<KeyValuePair<int, byte>> Patches { get; } = new List<KeyValuePair<int, byte>>();

        public IEnumerable<IDoorAotSetOpcode> Doors => Opcodes.OfType<IDoorAotSetOpcode>();
        public IEnumerable<SceEmSetOpcode> Enemies => Opcodes.OfType<SceEmSetOpcode>();
        public IEnumerable<IItemAotSetOpcode> Items => Opcodes.OfType<IItemAotSetOpcode>();
        public IEnumerable<AotResetOpcode> Resets => Opcodes.OfType<AotResetOpcode>();
        public IEnumerable<SceItemGetOpcode> ItemGets => Opcodes.OfType<SceItemGetOpcode>();
        public IEnumerable<XaOnOpcode> Sounds => Opcodes.OfType<XaOnOpcode>();

        public Rdt(RdtFile rdtFile, RdtId rdtId)
        {
            _rdtFile = rdtFile;
            RdtId = rdtId;
        }

        public IEnumerable<T> EnumerateOpcodes<T>(RandoConfig config) => AstEnumerator<T>.Enumerate(Ast!, config);

        public void SetDoorTarget(int id, RdtId target, DoorEntrance destination, RdtId originalId, bool noCompareRewrite = false)
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
            if (!noCompareRewrite)
            {
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
        }

        public void EnsureDoorUnlock(int id, byte lockId)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.LockId = lockId;
                    if (door.LockType == 0)
                        door.LockType = 254;
                }
            }
        }

        public void SetDoorLock(int id, byte lockId, byte lockType = 255)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id)
                {
                    door.LockId = lockId;
                    door.LockType = lockType;
                }
            }
        }

        public void SetDoorUnlock(int id, byte lockId) => SetDoorLock(id, lockId, 254);

        public void RemoveDoorUnlock(int id)
        {
            foreach (var door in Doors)
            {
                if (door.Id == id && door.LockType == 254)
                {
                    door.LockId = 0;
                    door.LockType = 0;
                }
            }
        }

        public void RemoveDoorLock(int id) => SetDoorLock(id, 0, 0);

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
                    enemy.Type = (byte)type;
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
                    if (!(opcode is UnknownOpcode unk))
                    {
                        unk = new UnknownOpcode(offset, 0, new byte[opcode.Length - 1]);
                        Opcodes[i] = unk;
                    }
                    unk.NopOut(Version);
                    break;
                }
            }
        }

        public DoorAotSeOpcode ConvertToDoor(int readOffset, int writeOffset)
        {
            var exMsg = $"Unable to find aot_set at offset 0x{readOffset:X}";

            var opcodeIndex = Array.FindIndex(Opcodes, x => x.Offset == readOffset);
            if (opcodeIndex == -1)
                throw new Exception(exMsg);

            var opcode = Opcodes[opcodeIndex];
            if (!(opcode is AotSetOpcode setOpcode))
                throw new Exception(exMsg);

            var door = new DoorAotSeOpcode();
            door.Offset = writeOffset;
            door.Length = 32;
            door.Opcode = (byte)OpcodeV2.DoorAotSe;
            door.Id = setOpcode.Id;
            door.SCE = 1;
            door.SAT = setOpcode.SAT;
            door.Floor = setOpcode.Floor;
            door.Super = setOpcode.Super;
            door.X = setOpcode.X;
            door.Z = setOpcode.Z;
            door.W = setOpcode.W;
            door.D = setOpcode.D;

            // We must remove any opcodes this overlaps
            var endOffset = writeOffset + door.Length;
            var opcodes = Opcodes.ToList();
            opcodes.RemoveAll(x => x.Offset >= writeOffset + 1 && x.Offset < endOffset);
            var nextOpcode = opcodes.FirstOrDefault(x => x.Offset >= endOffset);
            if (nextOpcode.Offset > endOffset)
            {
                var nopOpcode = new UnknownOpcode(endOffset, 0, new byte[nextOpcode.Offset - endOffset - 1]);
                opcodes.Insert(opcodeIndex + 1, nopOpcode);
            }
            opcodes[opcodeIndex] = door;
            Opcodes = opcodes.ToArray();

            return door;
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
                foreach (var patch in Patches)
                {
                    ms.Position = patch.Key;
                    bw.Write(patch.Value);
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
                    door.LockId,
                    door.LockType,
                    door.LockType == 0xFF ? "side" : IntelOrca.Biohazard.Items.GetItemName(door.LockType));
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
