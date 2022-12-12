using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard
{
    public class ADPCMDecoder
    {
        private const uint g_riffMagic = 0x46464952;
        private const uint g_waveMagic = 0x45564157;
        private const uint g_fmtMagic = 0x20746D66;
        private const uint g_dataMagic = 0x61746164;

        private const ushort g_tagPCM = 1;
        private const ushort g_tagADPCM = 2;

        private static short[] g_adaptationTable = new short[] {
            230, 230, 230, 230, 307, 409, 512, 614,
            768, 614, 512, 409, 307, 230, 230, 230,
        };

        private ushort _fmtTag;
        private ushort _channels;
        private uint _sampleRate;
        private uint _byteRate;
        private ushort _blockAlign;
        private ushort _bitsPerSample;

        private ushort _samplesPerBlock;
        private short[] _coefficient1 = new short[0];
        private short[] _coefficient2 = new short[0];

        private long _riffEnd;
        private long _dataPosition;
        private uint _dataLength;

        private int _samplesWritten;
        private long _writeRiffLengthPosition;
        private long _writeDataLengthPosition;

        public float Volume { get; set; } = 1.0f;

        public void Convert(string input, string output)
        {
            using (var fs = new FileStream(input, FileMode.Open, FileAccess.Read))
            {
                if (input.EndsWith(".sap"))
                {
                    fs.Position += 8;
                }
                using (var fsOutput = new FileStream(output, FileMode.Create))
                {
                    Convert(fs, fsOutput);
                }
            }
        }

        public void Convert(Stream input, Stream output)
        {
            var br = new BinaryReader(input);
            var bw = new BinaryWriter(output);

            ReadRiffHeader(br);
            while (input.Position < _riffEnd)
            {
                ReadChunk(br);
            }

            if ((_fmtTag == g_tagADPCM && _bitsPerSample != 4) ||
                (_fmtTag == g_tagPCM && _bitsPerSample != 16))
            {
                throw new Exception("Unsupported bit rate");
            }

            WriteRIFF(bw);
            WriteFmt(bw);
            WriteData(bw);
            if (_fmtTag == g_tagPCM)
                Copy(br, bw);
            else if (_fmtTag == g_tagADPCM)
                Decode(br, bw);
            Finalise(bw);
        }

        public double GetLength(string input)
        {
            using (var fs = new FileStream(input, FileMode.Open, FileAccess.Read))
            {
                if (input.EndsWith(".sap"))
                {
                    fs.Position += 8;
                }
                return GetLength(fs);
            }
        }

        public double GetLength(Stream input)
        {
            var br = new BinaryReader(input);

            ReadRiffHeader(br);
            while (input.Position < _riffEnd)
            {
                ReadChunk(br);
            }

            if ((_fmtTag == g_tagADPCM && _bitsPerSample != 4) ||
                (_fmtTag == g_tagPCM && _bitsPerSample != 16))
            {
                throw new Exception("Unsupported bit rate");
            }

            if (_fmtTag == g_tagPCM)
            {
                var length = _dataLength / (double)_byteRate;
                return length;
            }
            else if (_fmtTag == g_tagADPCM)
            {
                var numBlocks = _dataLength / _blockAlign;
                var numSamples = _samplesPerBlock * numBlocks;
                var length = numSamples / (double)_sampleRate;
                return length;
            }
            else
            {
                return 0;
            }
        }

        private void ReadRiffHeader(BinaryReader br)
        {
            var magic = br.ReadUInt32();
            var length = br.ReadUInt32();
            var kind = br.ReadUInt32();

            if (magic != g_riffMagic)
                throw new Exception("Invalid RIFF header");

            if (kind != g_waveMagic)
                throw new Exception("Unknown RIFF type");

            _riffEnd = br.BaseStream.Position + length - 4;
        }

        private void ReadChunk(BinaryReader br)
        {
            var magic = br.ReadUInt32();
            br.BaseStream.Position -= 4;
            switch (magic)
            {
                case g_fmtMagic:
                    ReadFmtChunk(br);
                    break;
                case g_dataMagic:
                    ReadDataChunk(br);
                    break;
                default:
                    ReadOtherChunk(br);
                    break;
            }
        }

        private void ReadFmtChunk(BinaryReader br)
        {
            var fmt = br.ReadUInt32();
            if (fmt != g_fmtMagic)
                throw new Exception("Invalid fmt header");

            var length = br.ReadUInt32();

            _fmtTag = br.ReadUInt16();
            _channels = br.ReadUInt16();
            _sampleRate = br.ReadUInt32();
            _byteRate = br.ReadUInt32();
            _blockAlign = br.ReadUInt16();
            _bitsPerSample = br.ReadUInt16();

            if (_fmtTag == 2)
            {
                var extraSize = br.ReadUInt16();
                _samplesPerBlock = br.ReadUInt16();
                var coefficientCount = br.ReadUInt16();
                var coefficient1 = new short[coefficientCount];
                var coefficient2 = new short[coefficientCount];
                for (var i = 0; i < coefficientCount; i++)
                {
                    coefficient1[i] = br.ReadInt16();
                    coefficient2[i] = br.ReadInt16();
                }
                _coefficient1 = coefficient1;
                _coefficient2 = coefficient2;
            }
        }

        private void ReadDataChunk(BinaryReader br)
        {
            var magic = br.ReadUInt32();
            if (magic != g_dataMagic)
                throw new Exception("Invalid data header");

            _dataLength = br.ReadUInt32();
            _dataPosition = br.BaseStream.Position;
            br.BaseStream.Position += _dataLength;
        }

        private void ReadOtherChunk(BinaryReader br)
        {
            br.ReadUInt32();
            var len = br.ReadUInt32();
            br.BaseStream.Position += len;
        }

        private void WriteRIFF(BinaryWriter bw)
        {
            bw.Write((uint)g_riffMagic);
            _writeRiffLengthPosition = bw.BaseStream.Position;
            bw.Write((uint)0);
            bw.Write((uint)g_waveMagic);
        }

        private void WriteFmt(BinaryWriter bw)
        {
            bw.Write((uint)g_fmtMagic);
            bw.Write((uint)16);
            bw.Write((ushort)1);
            bw.Write((ushort)_channels);
            bw.Write((uint)_sampleRate);
            bw.Write((uint)(_sampleRate * _channels * 2));
            bw.Write((ushort)(_channels * 2));
            bw.Write((ushort)16);
        }

        private void WriteData(BinaryWriter bw)
        {
            bw.Write((uint)g_dataMagic);
            _writeDataLengthPosition = bw.BaseStream.Position;
            bw.Write((uint)0);
        }

        private void Finalise(BinaryWriter bw)
        {
            bw.BaseStream.Position = _writeRiffLengthPosition;
            bw.Write(36 + (_samplesWritten * 2));
            bw.BaseStream.Position = _writeDataLengthPosition;
            bw.Write(_samplesWritten * 2);
        }

        private void Copy(BinaryReader br, BinaryWriter bw)
        {
            br.BaseStream.Position = _dataPosition;
            var buffer = new byte[4096];
            long offset = 0;
            while (offset < _dataLength)
            {
                var len = (int)Math.Min(_dataLength - offset, buffer.Length);
                br.Read(buffer, 0, len);
                if (Volume == 1)
                {
                    bw.Write(buffer, 0, len);
                }
                else
                {
                    var samples = MemoryMarshal.Cast<byte, short>(new ReadOnlySpan<byte>(buffer, 0, len));
                    WriteSamples(bw, samples);
                }
                _samplesWritten += len / 2;
                offset += len;
            }
        }

        private void Decode(BinaryReader br, BinaryWriter bw)
        {
            var channels = _channels;
            var coeff1 = new short[channels];
            var coeff2 = new short[channels];
            var delta = new short[channels];
            var sample1 = new short[channels];
            var sample2 = new short[channels];

            br.BaseStream.Position = _dataPosition;

            var blockSize = _blockAlign;
            for (long dataOffset = 0; dataOffset < _dataLength; dataOffset += blockSize)
            {
                // Read MS-ADPCM header
                for (var i = 0; i < channels; i++)
                {
                    var predictor = Clamp(br.ReadByte(), 0, 6);
                    coeff1[i] = _coefficient1[predictor];
                    coeff2[i] = _coefficient2[predictor];
                }

                for (var i = 0; i < channels; i++)
                    delta[i] = br.ReadInt16();
                for (var i = 0; i < channels; i++)
                    sample1[i] = br.ReadInt16();
                for (var i = 0; i < channels; i++)
                    sample2[i] = br.ReadInt16();

                for (int i = 0; i < channels; i++)
                    WriteSample(bw, sample2[i]);
                for (int i = 0; i < channels; i++)
                    WriteSample(bw, sample1[i]);

                // Decode
                var channel = 0;
                var offset = 7 * channels;
                while (offset < blockSize)
                {
                    var b = br.ReadByte();

                    var sample = ExpandNibble((byte)(b >> 4), channel);
                    WriteSample(bw, sample);
                    channel = (channel + 1) % channels;

                    sample = ExpandNibble((byte)(b & 0x0F), channel);
                    WriteSample(bw, sample);
                    channel = (channel + 1) % channels;

                    offset++;
                    _samplesWritten += 2;
                }
            }

            short ExpandNibble(byte nibble, int channel)
            {
                var signed = 8 <= nibble ? nibble - 16 : nibble;
                var a = sample1[channel] * coeff1[channel];
                var b = sample2[channel] * coeff2[channel];
                var predictor = ((a + b) >> 8) + (signed * delta[channel]);
                var predictorTruncated = (short)Clamp(predictor, short.MinValue, short.MaxValue);

                sample2[channel] = sample1[channel];
                sample1[channel] = predictorTruncated;

                delta[channel] = (short)((g_adaptationTable[nibble] * delta[channel]) / 256);
                if (delta[channel] < 16) delta[channel] = 16;

                return predictorTruncated;
            }
        }

        private void WriteSample(BinaryWriter bw, short sample)
        {
            if (Volume != 1)
                sample = (short)(sample * Volume);
            bw.Write(sample);
        }

        private void WriteSamples(BinaryWriter bw, ReadOnlySpan<short> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                bw.Write((short)(span[i] * Volume));
            }
        }

        private static int Clamp(int x, int min, int max)
        {
            return Math.Max(min, Math.Min(x, max));
        }
    }
}
