using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.BioRand.Process
{
    internal unsafe static class ProcessHelperExtensions
    {
        public static ReFlags ReadFlags(this IProcess process, int address, int numBytes)
        {
            var data = process.ReadArray<byte>(address, numBytes);
            return new ReFlags(data);
        }

        public static T Read<T>(this IProcess process, int address) where T : struct
        {
            var arr = process.ReadArray<T>(address, 1);
            return arr[0];
        }

        public static void Write<T>(this IProcess process, int address, T value) where T : struct
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            process.WriteArray(address, new ReadOnlySpan<T>(&value, 1));
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        }

        public static T[] ReadArray<T>(this IProcess process, int address, int count) where T : struct
        {
            var items = new T[count];
            process.ReadMemory(address, MemoryMarshal.Cast<T, byte>(items));
            return items;
        }

        public static void WriteArray<T>(this IProcess process, int address, ReadOnlySpan<T> value) where T : struct
        {
            process.WriteMemory(address, MemoryMarshal.Cast<T, byte>(value));
        }
    }
}
