using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard
{
    public class RdtFile
    {
        private readonly int[] _offsets;
        private readonly int[] _lengths;
        private readonly List<Range> _eventScripts = new List<Range>();
        private readonly List<Range> _emrs = new List<Range>();
        private readonly List<Range> _edds = new List<Range>();

        public BioVersion Version { get; }
        public byte[] Data { get; private set; }
        public ulong Checksum { get; }

        public RdtFile(string path)
            : this(null, path)
        {
        }

        public RdtFile(string path, BioVersion version)
            : this(version, path)
        {
        }

        private RdtFile(BioVersion? version, string path)
        {
            Data = File.ReadAllBytes(path);
            Version = version ?? DetectVersion(Data);
            _offsets = ReadHeader();
            _lengths = GetChunkLengths();
            GetNumEventScripts();
            Checksum = Data.CalculateFnv1a();
            if (version == BioVersion.Biohazard2)
            {
                ReadEMRs();
                // UpdateEMRs(1, 0.735911602209945);
                // UpdateEMRs(2, 1 / 0.735911602209945);
            }
        }

        private static BioVersion DetectVersion(byte[] data)
        {
            return data[0x12] == 0xFF && data[0x13] == 0xFF ?
                BioVersion.Biohazard1 :
                BioVersion.Biohazard2;
        }

        public void Save(string path)
        {
            File.WriteAllBytes(path, Data);
        }

        public MemoryStream GetStream()
        {
            return new MemoryStream(Data);
        }

        public int EventScriptCount => _eventScripts.Count;

        public byte[] GetScd(BioScriptKind kind, int scriptIndex = 0)
        {
            var index = GetScdChunkIndex(kind);
            var start = _offsets[index];
            var length = _lengths[index];
            if (kind == BioScriptKind.Event)
            {
                var range = _eventScripts[scriptIndex];
                var data = new byte[range.Length];
                Array.Copy(Data, start + range.Start, data, 0, range.Length);
                return data;
            }
            else
            {
                var data = new byte[length];
                Array.Copy(Data, start, data, 0, length);
                return data;
            }
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
                var start = _offsets[index];
                var length = _lengths[index];
                var end = _offsets[index] + length;
                var lengthDelta = data.Length - length;
                var sliceA = Data.Take(start).ToArray();
                var sliceB = Data.Skip(end).ToArray();
                for (int i = 0; i < _offsets.Length; i++)
                {
                    if (_offsets[i] != 0 && _offsets[i] > start)
                        _offsets[i] += lengthDelta;
                }
                _lengths[index] = data.Length;
                Data = sliceA.Concat(data).Concat(sliceB).ToArray();

                if (Version == BioVersion.Biohazard1)
                {
                    // Re-write ESP offsets
                    var ms = new MemoryStream(Data);
                    var br = new BinaryReader(ms);
                    var bw = new BinaryWriter(ms);
                    ms.Position = _offsets[14];
                    for (int i = 0; i < 8; i++)
                    {
                        var x = br.ReadInt32();
                        bw.BaseStream.Position -= 4;
                        if (x != -1)
                        {
                            var y = x + lengthDelta;
                            bw.Write(y);
                            bw.BaseStream.Position -= 4;
                        }
                        bw.BaseStream.Position -= 4;
                    }

                    // Re-write ESP offsets
                    ms.Position = Data.Length - 4;
                    for (int i = 0; i < 8; i++)
                    {
                        var x = br.ReadInt32();
                        bw.BaseStream.Position -= 4;
                        if (x != -1)
                        {
                            var y = x + lengthDelta;
                            bw.Write(y);
                            bw.BaseStream.Position -= 4;
                        }
                        bw.BaseStream.Position -= 4;
                    }
                }
                else
                {
                    // Re-write TIM offsets
                    var numEmbeddedTIMs = Data[2];
                    if (numEmbeddedTIMs != 0)
                    {
                        var ms = new MemoryStream(Data);
                        ms.Position = _offsets[10];
                        for (int i = 0; i < numEmbeddedTIMs; i++)
                        {
                            RewriteOffset(ms, start, lengthDelta);
                            RewriteOffset(ms, start, lengthDelta);
                        }
                    }
                }
                WriteOffsets();
            }
        }

        private void RewriteOffset(Stream stream, int min, int delta)
        {
            var br = new BinaryReader(stream);
            var bw = new BinaryWriter(stream);
            var offset = br.ReadInt32();
            if (offset != 0 && offset != -1 && offset >= min)
            {
                stream.Position -= 4;
                bw.Write(offset + delta);
            }
        }

        private int GetScdChunkIndex(BioScriptKind kind)
        {
            if (Version == BioVersion.Biohazard1)
            {
                switch (kind)
                {
                    case BioScriptKind.Init:
                        return 6;
                    case BioScriptKind.Main:
                        return 7;
                    case BioScriptKind.Event:
                        return 8;
                    default:
                        throw new ArgumentException("Invalid kind", nameof(kind));
                }
            }
            else
            {
                return kind == BioScriptKind.Init ? 16 : 17;
            }
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

        private int GetNumEventScripts()
        {
            if (Version != BioVersion.Biohazard1)
                return 0;

            var chunkIndex = GetScdChunkIndex(BioScriptKind.Event);
            var chunkOffset = _offsets[chunkIndex];
            var endOffset = chunkOffset + _lengths[chunkIndex];
            var ms = new MemoryStream(Data);
            ms.Position = chunkOffset;

            var br = new BinaryReader(ms);
            var offset = br.ReadInt32();
            var numScripts = offset / 4;
            _eventScripts.Clear();
            for (int i = 0; i < numScripts; i++)
            {
                var nextOffset = i == numScripts - 1 ? endOffset : br.ReadInt32();
                if (nextOffset == 0)
                {
                    var length = endOffset - chunkOffset - offset;
                    _eventScripts.Add(new Range(offset, length));
                    break;
                }
                else
                {
                    var length = nextOffset - offset;
                    _eventScripts.Add(new Range(offset, length));
                }
                offset = nextOffset;
            }
            return numScripts;
        }

        internal void ReadScript(BioScriptVisitor visitor)
        {
            visitor.VisitVersion(Version);
            ReadScript(BioScriptKind.Init, visitor);
            ReadScript(BioScriptKind.Main, visitor);
            ReadScript(BioScriptKind.Event, visitor);
        }

        private void ReadScript(BioScriptKind kind, BioScriptVisitor visitor)
        {
            var chunkIndex = GetScdChunkIndex(kind);
            var scriptOffset = _offsets[chunkIndex];
            if (scriptOffset == 0)
                return;

            if (kind == BioScriptKind.Event)
            {
                foreach (var eventScript in _eventScripts)
                {
                    var eventScriptOffset = scriptOffset + eventScript.Start;
                    var eventScriptLength = eventScript.Length;
                    var scdReader = new ScdReader();
                    scdReader.BaseOffset = scriptOffset + eventScript.Start;
                    scdReader.ReadScript(new ReadOnlyMemory<byte>(Data, eventScriptOffset, eventScriptLength), Version, kind, visitor);
                }
            }
            else
            {
                var scriptLength = _lengths[chunkIndex];
                var scdReader = new ScdReader();
                scdReader.BaseOffset = scriptOffset;
                scdReader.ReadScript(new ReadOnlyMemory<byte>(Data, scriptOffset, scriptLength), Version, kind, visitor);
            }
        }

        private void ReadEMRs()
        {
            var rbj = _offsets[22];
            if (rbj == 0)
                return;

            var ms = new MemoryStream(Data);
            var br = new BinaryReader(ms);
            var bw = new BinaryWriter(ms);

            ms.Position = rbj;
            var chunkLen = br.ReadInt32();
            var emrCount = br.ReadInt32();

            ms.Position = rbj + chunkLen;

            var offsets = new List<int>();
            for (int i = 0; i < emrCount * 2; i++)
            {
                offsets.Add(rbj + br.ReadInt32());
            }
            offsets.Add(rbj + chunkLen);

            for (int i = 0; i < emrCount; i++)
            {
                _emrs.Add(new Range(offsets[i * 2 + 0], offsets[i * 2 + 1] - offsets[i * 2 + 0]));
                _edds.Add(new Range(offsets[i * 2 + 1], offsets[i * 2 + 2] - offsets[i * 2 + 1]));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flagMask">1 = player, 2 = entity 255 (partner), 4 = entity 1, 8 = entity 2</param>
        /// <param name="yRatio"></param>
        private void UpdateEMRs(int flagMask, double yRatio)
        {
            var ms = new MemoryStream(Data);
            var br = new BinaryReader(ms);
            var bw = new BinaryWriter(ms);
            foreach (var emr in _emrs)
            {
                ms.Position = emr.Start;
                var flags = br.ReadUInt32();
                if ((flags & flagMask) == 0)
                    continue;

                var pArmature = br.ReadUInt16();
                var pFrames = br.ReadUInt16();
                var nArmature = br.ReadUInt16();
                var frameLen = br.ReadUInt16();

                var totalFrames = (emr.Length - 12) / 80;
                for (int i = 0; i < totalFrames; i++)
                {
                    var xOffset = br.ReadInt16();
                    var yOffset = br.ReadInt16();
                    if (yOffset != 0)
                    {
                        ms.Position -= 2;
                        bw.Write((short)(yOffset * yRatio));
                    }
                    var zOffset = br.ReadInt16();
                    var xSpeed = br.ReadInt16();
                    var ySpeed = br.ReadInt16();
                    var zSpeed = br.ReadInt16();

                    // Rotations
                    ms.Position += 68;
                }
            }
        }

        public string DecompileScd()
        {
            var scriptDecompiler = new ScriptDecompiler(false, false);
            ReadScript(scriptDecompiler);
            return scriptDecompiler.GetScript();
        }
    }

    [DebuggerDisplay("Start = {Start} Length = {Length}")]
    internal struct Range : IEquatable<Range>
    {
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;

        public Range(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public override bool Equals(object? obj)
        {
            return obj is Range range && Equals(range);
        }

        public bool Equals(Range other)
        {
            return Start == other.Start &&
                   Length == other.Length &&
                   End == other.End;
        }

        public override int GetHashCode()
        {
            int hashCode = -1042531914;
            hashCode = hashCode * -1521134295 + Start.GetHashCode();
            hashCode = hashCode * -1521134295 + Length.GetHashCode();
            hashCode = hashCode * -1521134295 + End.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Range left, Range right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Range left, Range right)
        {
            return !(left == right);
        }
    }
}
