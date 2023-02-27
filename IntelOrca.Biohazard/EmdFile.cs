﻿using System;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard
{
    public class EmdFile : ModelFile
    {
        private const int CHUNK_MESH = 14;

        private readonly byte[][] _chunks;

        public EmdFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);

                // Read header
                var directoryOffset = br.ReadInt32();
                var numOffsets = br.ReadInt32();

                // Read directory
                fs.Position = directoryOffset;
                var offsets = new int[numOffsets + 1];
                for (int i = 0; i < numOffsets; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                offsets[numOffsets] = directoryOffset;

                // Check all offsets are in order
                var lastOffset = 0;
                foreach (var offset in offsets)
                {
                    if (offset < lastOffset)
                        throw new NotSupportedException("Offsets not in order");
                    lastOffset = offset;
                }

                // Read chunks
                _chunks = new byte[numOffsets][];
                for (int i = 0; i < numOffsets; i++)
                {
                    var len = offsets[i + 1] - offsets[i];
                    fs.Position = offsets[i];
                    _chunks[i] = br.ReadBytes(len);
                }
            }
        }

        public void Save(string path)
        {
            var chunkSum = _chunks.Sum(x => x.Length);
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var bw = new BinaryWriter(fs);
                bw.Write(8 + chunkSum);
                bw.Write(_chunks.Length);
                for (int i = 0; i < _chunks.Length; i++)
                {
                    bw.Write(_chunks[i]);
                }

                var offset = 8;
                for (int i = 0; i < _chunks.Length; i++)
                {
                    bw.Write(offset);
                    offset += _chunks[i].Length;
                }
            }
        }

        public void Export(string path)
        {
            var meshChunk = _chunks[CHUNK_MESH];
            var ms = new MemoryStream(meshChunk);
            ExportObj(path, ms, 2);
        }
    }
}
