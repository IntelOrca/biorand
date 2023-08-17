using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using static IntelOrca.Biohazard.BioRand.Cli.Win32;

namespace IntelOrca.Biohazard.BioRand.Cli
{
    internal unsafe class ReProcess
    {
        private readonly Process _process;

        public static ReProcess Find()
        {
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                if (process.ProcessName.Contains("bio2"))
                {
                    return new ReProcess(process);
                }
            }
            return null;
        }

        public ReProcess(Process process)
        {
            _process = process;
        }

        public int CurrentStage => Read<ushort>(0x0098EB14);
        public int CurrentRoom => Read<ushort>(0x0098EB16);
        public int CurrentCut => Read<ushort>(0x0098EB18);
        public int LastCut => Read<ushort>(0x0098EB1A);
        public ReFlags GameFlags => ReadFlags(0x00989ED0, 4);
        public ReFlags StateFlags => ReadFlags(0x00989ED4, 4);
        public ReFlags GeneralFlags => ReadFlags(0x0098EB4C, 32);
        public ReFlags LocalFlags => ReadFlags(0x0098EB6C, 8);
        public ReFlags ItemFlags => ReadFlags(0x0098EBB4, 32);
        public ReFlags LockFlags => ReadFlags(0x0098ED2C, 8);
        public byte HudMode => Read<byte>(0x00691F70);
        public byte PickupName
        {
            get => Read<byte>(0x0098504F);
            set => Write<byte>(0x0098504F, value);
        }
        public byte PickupType
        {
            get => Read<byte>(0x0098E529);
            set => Write<byte>(0x0098E529, value);
        }
        public uint PickupAotAddress => Read<uint>(0x009888D0);

        public byte PickupAmount
        {
            get
            {
                var address = PickupAotAddress;
                return Read<byte>((int)(address + 14));
            }
            set
            {
                var address = PickupAotAddress;
                Write<byte>((int)(address + 14), value);
            }
        }

        public byte PickupFlag
        {
            get
            {
                var address = PickupAotAddress;
                return Read<byte>((int)(address + 16));
            }
            set
            {
                var address = PickupAotAddress;
                Write<byte>((int)(address + 16), value);
            }
        }

        public int GetTime()
        {
            var time = ReadArray<int>(0x00680588, 2);
            return (time[0] * 1000) + ((time[1] * 1000) / 60);
        }

        public ItemBox GetItemBox()
        {
            var items = ReadArray<ReItem>(0x0098ED60, 64);
            return new ItemBox(items);
        }

        public void SetItemBox(ItemBox itemBox)
        {
            WriteArray<ReItem>(0x0098ED60, itemBox.Items);
        }

        public ReFlags ReadFlags(int address, int numBytes)
        {
            var data = ReadArray<byte>(address, numBytes);
            return new ReFlags(data);
        }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        private T Read<T>(int address) where T : struct
        {
            var arr = ReadArray<T>(address, 1);
            return arr[0];
        }

        private void Write<T>(int address, T value) where T : struct
        {
            WriteArray(address, new ReadOnlySpan<T>(&value, 1));
        }

        private T[] ReadArray<T>(int address, int count) where T : struct
        {
            var items = new T[count];
            var numBytes = count * sizeof(T);
            fixed (T* item = &items[0])
            {
                ReadProcessMemory(_process.Handle, (IntPtr)address, (IntPtr)item, (IntPtr)numBytes, out var readBytes);
            }
            return items;
        }

        private void WriteArray<T>(int address, ReadOnlySpan<T> value) where T : struct
        {
            var numBytes = value.Length * sizeof(T);
            fixed (T* item = &value[0])
            {
                WriteProcessMemory(_process.Handle, (IntPtr)address, (IntPtr)item, (IntPtr)numBytes, out var writtenBytes);
            }
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    public readonly struct ReFlags : IEquatable<ReFlags>
    {
        private readonly byte[] _flags;

        public int Count => _flags == null ? 0 : _flags.Length * 8;
        public int ByteLength => _flags == null ? 0 : _flags.Length;

        public ReFlags(ReadOnlySpan<byte> src)
        {
            _flags = src.ToArray();
        }

        public int[] Keys
        {
            get
            {
                var keys = new List<int>();
                var numFlags = Count;
                for (var i = 0; i < numFlags; i++)
                {
                    if (this[i])
                        keys.Add(i);
                }
                return keys.ToArray();
            }
        }

        // public bool this[int index] => (_flags[index >> 3] & (1 << (index & 7))) != 0;
        public bool this[int index]
        {
            get
            {
                var ints = MemoryMarshal.Cast<byte, int>(_flags);
                var idx = ints[index >> 5];
                var mask = 1 << ((32 - index - 1) & 31);
                return (idx & mask) != 0;
            }
        }
        public static bool operator ==(ReFlags lhs, ReFlags rhs) => lhs.Equals(rhs);
        public static bool operator !=(ReFlags lhs, ReFlags rhs) => !(lhs == rhs);

        public static ReFlags operator ^(ReFlags lhs, ReFlags rhs)
        {
            var minLength = Math.Min(lhs.ByteLength, rhs.ByteLength);
            var resultFlags = new byte[minLength];
            for (var i = 0; i < minLength; i++)
            {
                resultFlags[i] = (byte)(lhs._flags[i] ^ rhs._flags[i]);
            }
            return new ReFlags(resultFlags);
        }

        public static ReFlags operator &(ReFlags lhs, ReFlags rhs)
        {
            var minLength = Math.Min(lhs.ByteLength, rhs.ByteLength);
            var resultFlags = new byte[minLength];
            for (var i = 0; i < minLength; i++)
            {
                resultFlags[i] = (byte)(lhs._flags[i] & rhs._flags[i]);
            }
            return new ReFlags(resultFlags);
        }

        public override bool Equals(object obj) => obj is ReFlags flags && Equals(flags);

        public bool Equals(ReFlags other)
        {
            if (_flags is null && other._flags is null)
                return true;
            if (_flags is null || other._flags is null)
                return false;
            return _flags.SequenceEqual(other._flags);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                foreach (byte flag in _flags)
                {
                    hash = hash * 31 + flag.GetHashCode();
                }
            }
            return hash;
        }
    }
}
