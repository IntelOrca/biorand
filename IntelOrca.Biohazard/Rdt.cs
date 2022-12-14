using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class Rdt
    {
        private RdtFile _rdtFile;
        private List<(EmrFlags, double)> _emrScales = new List<(EmrFlags, double)>();

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
        public List<OpcodeBase> AdditionalOpcodes { get; } = new List<OpcodeBase>();

        public IEnumerable<OpcodeBase> AllOpcodes => Opcodes.Concat(AdditionalOpcodes);
        public IEnumerable<IDoorAotSetOpcode> Doors => AllOpcodes.OfType<IDoorAotSetOpcode>();
        public IEnumerable<SceEmSetOpcode> Enemies => AllOpcodes.OfType<SceEmSetOpcode>();
        public IEnumerable<IItemAotSetOpcode> Items => AllOpcodes.OfType<IItemAotSetOpcode>();
        public IEnumerable<AotResetOpcode> Resets => AllOpcodes.OfType<AotResetOpcode>();
        public IEnumerable<SceItemGetOpcode> ItemGets => AllOpcodes.OfType<SceItemGetOpcode>();
        public IEnumerable<XaOnOpcode> Sounds => AllOpcodes.OfType<XaOnOpcode>();

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

        public DoorAotSeOpcode ConvertToDoor(byte id, byte texture)
        {
            var aotSet = Opcodes.OfType<AotSetOpcode>().FirstOrDefault(x => x.Id == id);
            if (aotSet == null)
                throw new Exception($"Failed to find aot_set for id {id}");

            var door = new DoorAotSeOpcode()
            {
                Length = 32,
                Opcode = (byte)OpcodeV2.DoorAotSe,
                Id = aotSet.Id,
                SCE = 1,
                SAT = aotSet.SAT,
                Floor = aotSet.Floor,
                Super = aotSet.Super,
                X = aotSet.X,
                Z = aotSet.Z,
                W = aotSet.W,
                D = aotSet.D,
                Texture = texture
            };
            AdditionalOpcodes.Add(door);
            Nop(aotSet.Offset);
            return door;
        }

        public int? ScaleEmrY(EmrFlags flags, double scale)
        {
            if (_emrScales.Any(x => x.Item1 == flags))
                return null;

            for (int i = 0; i < _rdtFile.EmrCount; i++)
            {
                var emrFlags = _rdtFile.GetEmrFlags(i);
                if ((emrFlags & flags) != 0)
                {
                    _emrScales.Add((flags, scale));
                    return i;
                }
            }
            return null;
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

            UpdateEmrs();
            PrependOpcodes();

            Directory.CreateDirectory(Path.GetDirectoryName(ModifiedPath!)!);
            File.WriteAllBytes(ModifiedPath!, _rdtFile.Data);
        }

        private void UpdateEmrs()
        {
            foreach (var (flags, scale) in _emrScales)
            {
                for (int i = 0; i < _rdtFile.EmrCount; i++)
                {
                    var emrFlags = _rdtFile.GetEmrFlags(i);
                    if (emrFlags == flags)
                    {
                        _rdtFile.ScaleEmrYs(i, scale);
                        continue;
                    }
                    else if ((emrFlags & flags) != 0)
                    {
                        var newIndex = _rdtFile.DuplicateEmr(i);
                        _rdtFile.SetEmrFlags(i, flags);
                        _rdtFile.SetEmrFlags(newIndex, emrFlags & ~flags);
                        _rdtFile.ScaleEmrYs(i, scale);
                        continue;
                    }
                }
            }

            // if (RdtId == new RdtId(5, 0x12) && _emrScales.Count != 0)
            // {
            //     _rdtFile.DuplicateEmr(0);
            //     _rdtFile.UpdateEmrFlags(0, EmrFlags.Player);
            //     _rdtFile.UpdateEmrFlags(1, EmrFlags.Entity1 | EmrFlags.Entity2);
            // }
        }

        private void PrependOpcodes()
        {
            if (AdditionalOpcodes.Count == 0)
                return;

            var initScd = _rdtFile.GetScd(BioScriptKind.Init);
            var ms = new MemoryStream(initScd);
            var br = new BinaryReader(ms);
            var firstSub = br.ReadUInt16();
            var numSubs = firstSub / 2;
            var subPositions = new ushort[numSubs];
            subPositions[0] = firstSub;
            for (int i = 1; i < numSubs; i++)
                subPositions[i] = br.ReadUInt16();

            var newMs = new MemoryStream();

            // Move to first sub and write new opcodes
            newMs.Position = firstSub;
            var bw = new BinaryWriter(newMs);
            foreach (var opcode in AdditionalOpcodes)
            {
                opcode.Write(bw);
            }
            var increaseDelta = newMs.Position - firstSub;

            bw.Write(initScd.Skip(firstSub).ToArray());

            // Write sub offset table
            newMs.Position = 0;
            bw.Write(firstSub);
            for (int i = 1; i < numSubs; i++)
            {
                bw.Write((ushort)(subPositions[i] + increaseDelta));
            }

            _rdtFile.SetScd(BioScriptKind.Init, newMs.ToArray());
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
