using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard
{
    public class RdtFile
    {
        private readonly int[] _offsets;
        private readonly int[] _lengths;

        public BioVersion Version { get; }
        public byte[] Data { get; private set; }
        public ulong Checksum { get; }

        public RdtFile(string path, BioVersion version)
        {
            Version = version;
            Data = File.ReadAllBytes(path);
            _offsets = ReadHeader();
            _lengths = GetChunkLengths();
            Checksum = Data.CalculateFnv1a();
        }

        public void Save(string path)
        {
            File.WriteAllBytes(path, Data);
        }

        public MemoryStream GetStream()
        {
            return new MemoryStream(Data);
        }

        public byte[] GetScd(BioScriptKind kind)
        {
            var index = GetScdChunkIndex(kind);
            var start = _offsets[index];
            var length = _lengths[index];
            var data = new byte[length];
            Array.Copy(Data, start, data, 0, length);
            return data;
        }

        public void SetScd(BioScriptKind kind, byte[] data)
        {
            var index = GetScdChunkIndex(kind);
            if (_lengths[index] == data.Length)
            {
                var start = _offsets[index];
                Array.Copy(data, 0, Data, start, data.Length);
            }
            else
            {
                _offsets[index] = Data.Length;
                _lengths[index] = data.Length;
                Data = Data.Concat(data).ToArray();
                WriteOffsets();

                // var start = _offsets[index];
                // var length = _lengths[index];
                // var end = _offsets[index] + length;
                // var sliceA = Data.Take(start).ToArray();
                // var sliceB = Data.Skip(end).ToArray();
                // for (int i = 0; i < _offsets.Length; i++)
                // {
                //     if (_offsets[i] > start)
                //         _offsets[i] -= length;
                // }
                // _offsets[index] = Data.Length;
                // _lengths[index] = data.Length;
                // Data = sliceA.Concat(sliceB).Concat(data).ToArray();
                // WriteOffsets();
            }
        }

        private int GetScdChunkIndex(BioScriptKind kind)
        {
            int index;
            if (Version == BioVersion.Biohazard1)
            {
                index = kind == BioScriptKind.Init ? 6 : 7;
            }
            else
            {
                index = kind == BioScriptKind.Init ? 16 : 17;
            }
            return index;
        }

        private int[] ReadHeader()
        {
            if (Data.Length <= 8)
                return new int[0];

            var br = new BinaryReader(new MemoryStream(Data));
            if (Version == BioVersion.Biohazard1)
            {
                br.ReadBytes(12);
                br.ReadBytes(20 * 3);

                var offsets = new int[19];
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                return offsets;
            }
            else
            {
                br.ReadBytes(8);

                var offsets = new int[23];
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                return offsets;
            }
        }

        private void WriteOffsets()
        {
            var bw = new BinaryWriter(new MemoryStream(Data));
            if (Version == BioVersion.Biohazard1)
            {
                bw.BaseStream.Position = 12 + (20 * 3);
            }
            else
            {
                bw.BaseStream.Position = 8;
            }
            for (int i = 0; i < _offsets.Length; i++)
            {
                bw.Write(_offsets[i]);
            }
        }

        private int[] GetChunkLengths()
        {
            var lengths = new int[_offsets.Length];
            for (int i = 0; i < _offsets.Length; i++)
            {
                var start = _offsets[i];
                var end = Data.Length;
                for (int j = 0; j < _offsets.Length; j++)
                {
                    var o = _offsets[j];
                    if (o > start && o < end)
                    {
                        end = o;
                    }
                }
                lengths[i] = end - start;
            }
            return lengths;
        }

        internal void ReadScript(BioScriptVisitor visitor)
        {
            visitor.VisitVersion(Version);
            ReadScript(BioScriptKind.Init, visitor);
            ReadScript(BioScriptKind.Main, visitor);
        }

        private void ReadScript(BioScriptKind kind, BioScriptVisitor visitor)
        {
            var chunkIndex = GetScdChunkIndex(kind);
            var scriptOffset = _offsets[chunkIndex];
            if (scriptOffset == 0)
                return;

            var scriptLength = _lengths[chunkIndex];
            var scdReader = new ScdReader();
            scdReader.BaseOffset = scriptOffset;
            scdReader.ReadScript(new ReadOnlyMemory<byte>(Data, scriptOffset, scriptLength), Version, kind, visitor);
        }
    }
}
