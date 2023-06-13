using System;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard
{
    public abstract class ModelFile
    {
        private readonly byte[][] _chunks;

        public BioVersion Version { get; }
        protected abstract int Md1ChunkIndex { get; }
        protected abstract int Md2ChunkIndex { get; }
        public abstract int NumPages { get; }

        public ModelFile(BioVersion version, string path)
        {
            Version = version;
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

        protected byte[] GetChunk(int index) => _chunks[index];
        protected void SetChunk(int index, byte[] value) => _chunks[index] = value;

        public Md1 Md1
        {
            get
            {
                if (Version != BioVersion.Biohazard2)
                    throw new InvalidOperationException();
                return new Md1(_chunks[Md1ChunkIndex]);
            }
            set
            {
                if (Version != BioVersion.Biohazard2)
                    throw new InvalidOperationException();
                _chunks[Md1ChunkIndex] = value.GetBytes();
            }
        }

        public Md2 Md2
        {
            get
            {
                if (Version != BioVersion.Biohazard3)
                    throw new InvalidOperationException();
                return new Md2(_chunks[Md2ChunkIndex]);
            }
            set
            {
                if (Version != BioVersion.Biohazard3)
                    throw new InvalidOperationException();
                _chunks[Md2ChunkIndex] = value.GetBytes();
            }
        }

        public abstract Emr GetEmr(int index);
        public abstract void SetEmr(int index, Emr emr);

        public static ModelFile? FromFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);
                var directoryOffset = br.ReadInt32();
                var numOffsets = br.ReadInt32();
                fs.Close();
                switch (numOffsets)
                {
                    case 4:
                        return new PldFile(BioVersion.Biohazard2, path);
                    case 5:
                        return new PldFile(BioVersion.Biohazard3, path);
                    case 8:
                        return new EmdFile(BioVersion.Biohazard2, path);
                    case 15:
                        return new EmdFile(BioVersion.Biohazard3, path);
                    default:
                        throw new InvalidDataException("Unsupported file type");
                }
            }
        }
    }
}
