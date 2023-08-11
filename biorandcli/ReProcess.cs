using System;
using System.Diagnostics;
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
        public ulong GameFlags => Read<ulong>(0x00989ED0);
        public ulong StateFlags => Read<ulong>(0x00989ED4);
        public ulong GeneralFlags => Read<ulong>(0x0098EB4C);
        public ulong LocalFlags => Read<ulong>(0x0098EB6C);
        public ulong ItemFlags => Read<ulong>(0x0098EBB4);
        public ulong LockFlags => Read<ulong>(0x0098ED2C);

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
            var numBytes = count * sizeof(ReItem);
            fixed (T* item = &items[0])
            {
                ReadProcessMemory(_process.Handle, (IntPtr)address, (IntPtr)item, (IntPtr)numBytes, out var readBytes);
            }
            return items;
        }

        private void WriteArray<T>(int address, ReadOnlySpan<T> value) where T : struct
        {
            var numBytes = value.Length * sizeof(ReItem);
            fixed (T* item = &value[0])
            {
                WriteProcessMemory(_process.Handle, (IntPtr)address, (IntPtr)item, (IntPtr)numBytes, out var writtenBytes);
            }
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }
}