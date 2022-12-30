using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard
{
    public class SapFile
    {
        private const uint g_riffMagic = 0x46464952;

        public ulong Header { get; set; }
        public List<byte[]> WavFiles { get; } = new List<byte[]>();

        public SapFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);
                Header = br.ReadUInt64();
                while (fs.Position != fs.Length)
                {
                    var riffMagic = br.ReadUInt32();
                    if (riffMagic != g_riffMagic)
                    {
                        throw new InvalidOperationException($"Failed to process '{path}'.");
                    }

                    var wavLength = (int)(br.ReadUInt32() + 8);
                    fs.Position -= 8;
                    var wav = br.ReadBytes(wavLength);
                    WavFiles.Add(wav);
                }
            }
        }

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var bw = new BinaryWriter(fs);
                bw.Write(Header);
                foreach (var wavFile in WavFiles)
                {
                    bw.Write(wavFile);
                }
            }
        }
    }
}
