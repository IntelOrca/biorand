using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    /// <summary>
    /// Represents a .VB sound data file from Resident Evil 3.
    /// </summary>
    internal class VabFile
    {
        private readonly byte[] _head;
        private readonly byte[] _split;
        private readonly byte[] _tail;
        private readonly List<byte[]> _samples = new List<byte[]>();

        public VabFile(string path)
            : this(File.ReadAllBytes(path))
        {
        }

        public VabFile(byte[] data)
        {
            _head = data.Take(32).ToArray();
            _split = data.Skip(32).Take(16).ToArray();
            var start = 32 + 16;
            for (int i = start; i < data.Length; i++)
            {
                if (Compare(data, i, _split))
                {
                    var end = i;
                    var block = data.Skip(start).Take(end - start).ToArray();
                    _samples.Add(block);

                    start = i + _split.Length;
                }
            }
            _tail = data.Skip(start).ToArray();
        }

        public void SetSampleFromADPCM(int index, byte[] data)
        {
            _samples[index] = data;
        }

        public void SetSampleFromPCM(int index, byte[] data)
        {
            var encoder = new ADPCMEncoder();
            var adpcm = encoder.Encode(MemoryMarshal.Cast<byte, short>(data));
            SetSampleFromADPCM(index, adpcm);
        }

        public void SetSampleFromWaveFile(int index, string wavPath)
        {
            var wav = File.ReadAllBytes(wavPath);
            var pcm = wav.Skip(0x2C).ToArray();
            SetSampleFromPCM(index, pcm);
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var bw = new BinaryWriter(fs);
                bw.Write(_head);
                foreach (var sample in _samples)
                {
                    bw.Write(_split);
                    bw.Write(sample);
                }
                bw.Write(_split);
                bw.Write(_tail);
            }
        }

        private static bool Compare(byte[] haystack, int offset, byte[] needle)
        {
            if (offset + needle.Length > haystack.Length)
                return false;

            for (int i = 0; i < needle.Length; i++)
            {
                if (haystack[offset + i] != needle[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
