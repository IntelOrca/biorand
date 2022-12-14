using System;
using System.IO;
using NVorbis;

namespace IntelOrca.Biohazard
{
    internal class WaveformBuilder
    {
        private const uint g_riffMagic = 0x46464952;
        private const uint g_waveMagic = 0x45564157;
        private const uint g_fmtMagic = 0x20746D66;
        private const uint g_dataMagic = 0x61746164;

        private MemoryStream _stream = new MemoryStream();
        private WaveHeader _header;
        private bool _headerWritten;
        private bool _finished;
        private double _initialSilence;

        public WaveformBuilder()
        {
        }

        private void WriteHeader(in WaveHeader header)
        {
            if (_headerWritten)
                throw new InvalidOperationException();

            var bw = new BinaryWriter(_stream);
            bw.Write(header);
            _header = header;
            _headerWritten = true;

            AppendSilence(_initialSilence);
        }

        private void Finish()
        {
            var stream = _stream;
            var bw = new BinaryWriter(stream);
            stream.Position = 4;
            bw.Write((uint)(stream.Length - 8));
            stream.Position = 40;
            bw.Write((uint)(stream.Length - 44));
            stream.Position = stream.Length;
        }

        public void Save(string path, ulong sapHeader = 1)
        {
            if (!_headerWritten)
                throw new InvalidOperationException();

            if (!_finished)
                Finish();

            using (var fs = new FileStream(path, FileMode.Create))
            {
                if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                {
                    var bw = new BinaryWriter(fs);
                    bw.Write(sapHeader);
                }
                _stream.Position = 0;
                _stream.CopyTo(fs);
                _stream.Position = _stream.Length;
            }
        }

        public void SaveAppend(string path)
        {
            if (!_headerWritten)
                throw new InvalidOperationException();

            if (!_finished)
                Finish();

            using (var fs = new FileStream(path, FileMode.Append))
            {
                _stream.Position = 0;
                _stream.CopyTo(fs);
                _stream.Position = _stream.Length;
            }
        }

        public void AppendSilence(double length)
        {
            if (length <= 0)
                return;

            if (!_headerWritten)
            {
                _initialSilence += length;
                return;
            }

            var numBytes = AlignTime(in _header, length);
            if (numBytes <= 0)
                return;

            var bytes = new byte[4096];
            while (numBytes != 0)
            {
                var left = Math.Min(bytes.Length, numBytes);
                _stream.Write(bytes, 0, left);
                numBytes -= left;
            }
        }

        public void Append(string path)
        {
            Append(path, 0, double.NaN);
        }

        public void Append(string path, double start, double end)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                {
                    fs.Position += 8;

                    var ms = new MemoryStream();
                    var decoder = new ADPCMDecoder();
                    decoder.Convert(fs, ms);

                    ms.Position = 0;
                    Append(ms, start, end);
                }
                else if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                {
                    AppendOgg(fs, start, end);
                }
                else
                {
                    Append(fs, start, end);
                }
            }
        }

        public void Append(Stream input, double start, double end)
        {
            var br = new BinaryReader(input);
            var header = br.ReadStruct<WaveHeader>();

            if (!_headerWritten)
            {
                WriteHeader(in header);
            }

            var startOffset = AlignTime(in header, start);
            var endOffset = AlignTime(in header, end);
            var length = endOffset - startOffset;
            if (length > 0)
            {
                input.Position += startOffset;
                input.CopyAmountTo(_stream, length);
            }
        }

        public void AppendOgg(Stream input, double start, double end)
        {
            using (var vorbis = new VorbisReader(input))
            {
                if (!_headerWritten)
                {
                    AppendCustomHeader(vorbis);
                }

                if (start != 0)
                    vorbis.SeekTo(TimeSpan.FromSeconds(start));

                // Stream samples from ogg
                var maxSamplesToRead = double.IsNaN(end) ? int.MaxValue : (int)(vorbis.Channels * vorbis.SampleRate * end);

                var bw = new BinaryWriter(_stream);
                int readSamples;
                var readBuffer = new float[vorbis.Channels * vorbis.SampleRate / 8];
                while ((readSamples = vorbis.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    var leftToRead = Math.Min(readSamples, maxSamplesToRead);
                    for (int i = 0; i < leftToRead; i++)
                    {
                        var value = (short)(readBuffer[i] * short.MaxValue);
                        bw.Write(value);
                    }
                    maxSamplesToRead -= leftToRead;
                }
            }
        }

        private void AppendCustomHeader(VorbisReader vorbis)
        {
            _header.nRiffMagic = g_riffMagic;
            _header.nRiffLength = 0;
            _header.nWaveMagic = g_waveMagic;
            _header.nFormatMagic = g_fmtMagic;
            _header.nFormatLength = 16;
            _header.wFormatTag = 1;
            _header.nChannels = (ushort)vorbis.Channels;
            _header.nSamplesPerSec = (uint)vorbis.SampleRate;
            _header.nAvgBytesPerSec = (uint)(vorbis.SampleRate * 16 * vorbis.Channels) / 8;
            _header.nBlockAlign = (ushort)((16 * vorbis.Channels) / 8);
            _header.wBitsPerSample = 16;
            _header.wDataMagic = g_dataMagic;
            _header.nDataLength = 0;

            var bw = new BinaryWriter(_stream);
            bw.Write(_header);
            _headerWritten = true;
        }

        private static int AlignTime(in WaveHeader header, double t)
        {
            if (double.IsNaN(t))
                return (int)header.nDataLength;

            var precise = header.nAvgBytesPerSec * t;
            var result = ((int)precise / header.nBlockAlign) * header.nBlockAlign;
            if (result > header.nDataLength)
            {
                result = (int)header.nDataLength;
            }
            return result;
        }
    }
}
