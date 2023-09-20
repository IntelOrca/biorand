using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.BioRand.Process
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ReItem
    {
        public byte Type;
        public byte Amount;
        public byte Size;
        public byte zAlign;

        public override string ToString() => $"{Type} x{Amount}";
    }
}
