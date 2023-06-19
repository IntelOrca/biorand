using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 44)]
    public struct WaveHeader
    {
        public const uint RiffMagic = 0x46464952;
        public const uint WaveMagic = 0x45564157;
        public const uint FmtMagic = 0x20746D66;
        public const uint DataMagic = 0x61746164;

        public uint nRiffMagic;
        public uint nRiffLength;
        public uint nWaveMagic;
        public uint nFormatMagic;
        public uint nFormatLength;
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public uint wDataMagic;
        public uint nDataLength;
    }
}
