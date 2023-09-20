using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.BioRand
{
    internal static class Win32
    {
        [DllImport("kernel32.dll")]
        public extern static bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public extern static bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);
    }
}
