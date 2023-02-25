using System;
using System.IO;
using System.Numerics;
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

        public int Channels { get; private set; }
        public int SampleRate { get; private set; }
        public float Volume { get; private set; } = 1;

        public static bool IsSupportedExtension(string path)
        {
            return path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        }

        public WaveformBuilder(int channels = 0, int sampleRate = 0, float volume = 1)
        {
            Channels = channels;
            SampleRate = sampleRate;
            Volume = volume;
        }

        private void WriteHeader(in WaveHeader header)
        {
            if (_headerWritten)
                throw new InvalidOperationException();

            var bw = new BinaryWriter(_stream);
            _header = header;
            if (Channels != 0)
            {
                _header.nChannels = (ushort)Channels;
            }
            if (SampleRate != 0)
            {
                _header.nSamplesPerSec = (uint)SampleRate;
            }
            if (Channels != 0 || SampleRate != 0)
            {
                _header.nBlockAlign = (ushort)((_header.nChannels * _header.wBitsPerSample) / 8);
                _header.nAvgBytesPerSec = _header.nBlockAlign * _header.nSamplesPerSec;
            }
            bw.Write(_header);
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
            _finished = true;
        }

        public unsafe byte[] GetPCM()
        {
            var ms = new MemoryStream();
            _stream.Position = sizeof(WaveHeader);
            _stream.CopyTo(ms);
            _stream.Position = _stream.Length;
            return ms.ToArray();
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

        public void SaveAt(string path, int sapIndex)
        {
            if (!_headerWritten)
                throw new InvalidOperationException();

            if (!_finished)
                Finish();

            if (!path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException();
            }

            _stream.Position = 0;
            var newSample = _stream.ToArray();
            _stream.Position = _stream.Length;

            var sapFile = new SapFile(path);
            sapFile.WavFiles[sapIndex] = newSample;
            sapFile.Save(path);
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
                Append(path, fs, start, end);
            }
        }

        public void Append(string path, int sapIndex, double start, double end)
        {
            var sapFile = new SapFile(path);
            var wavFile = sapFile.WavFiles[sapIndex];
            var sapStream = new MemoryStream(wavFile);
            var wavStream = new MemoryStream();
            var decoder = new ADPCMDecoder();
            decoder.Convert(sapStream, wavStream);
            wavStream.Position = 0;
            AppendWav(wavStream, start, end);
        }

        public void Append(string path, Stream input) => Append(path, input, 0, double.NaN);

        public void Append(string path, Stream input, double start, double end)
        {
            if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
            {
                input.Position += 8;

                var ms = new MemoryStream();
                var decoder = new ADPCMDecoder();
                decoder.Convert(input, ms);

                ms.Position = 0;
                AppendWav(ms, start, end);
            }
            else if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                AppendOgg(input, start, end);
            }
            else
            {
                AppendWav(input, start, end);
            }
        }

        public void AppendWav(Stream input, double start, double end)
        {
            var initialPosition = input.Position;
            var br = new BinaryReader(input);
            var header = br.ReadStruct<WaveHeader>();
            if (header.wFormatTag == 2)
            {
                input.Position = initialPosition;
                var ms = new MemoryStream();
                var decoder = new ADPCMDecoder();
                decoder.Convert(input, ms);
                ms.Position = 0;
                AppendWav(ms, start, end);
                return;
            }

            if (header.nChannels != 1 && header.nChannels != 2)
                throw new NotSupportedException("Only mono or stereo sound can be converted.");

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
                if (Volume == 1 && _header.nSamplesPerSec == header.nSamplesPerSec && _header.nChannels == header.nChannels)
                {
                    input.CopyAmountTo(_stream, length);
                }
                else
                {
                    var bw = new BinaryWriter(_stream);
                    if (header.nSamplesPerSec != _header.nSamplesPerSec)
                    {
                        var resampleStream = new MemoryStream();
                        bw = new BinaryWriter(resampleStream);
                    }
                    if (header.nChannels > _header.nChannels)
                    {
                        for (int i = 0; i < length; i += 4)
                        {
                            var left = br.ReadInt16();
                            var right = br.ReadInt16();
                            var sample = (short)((left + right) / 2);
                            bw.Write((short)(sample * Volume));
                        }
                    }
                    else if (header.nChannels < _header.nChannels)
                    {
                        for (int i = 0; i < length; i += 2)
                        {
                            var sampleIn = br.ReadInt16();
                            var sampleOut = (short)(sampleIn * Volume);
                            bw.Write(sampleOut);
                            bw.Write(sampleOut);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < length; i += 2)
                        {
                            var sample = br.ReadInt16();
                            bw.Write((short)(sample * Volume));
                        }
                    }
                    if (header.nSamplesPerSec != _header.nSamplesPerSec)
                    {
                        var inStream = bw.BaseStream;
                        inStream.Position = 0;
                        var inSamples = (int)(inStream.Length / 2);
                        var factor = (double)_header.nSamplesPerSec / header.nSamplesPerSec;
                        Resample(inStream, _stream, inSamples, factor);
                    }
                }
            }
        }

        private void Resample(Stream input, Stream output, int inSamples, double factor)
        {
            var br = new BinaryReader(input);
            var bw = new BinaryWriter(output);
            var outSamples = (int)(inSamples * factor);
            var mu = 0.0;
            var rate = 1 / factor;
            for (int i = 0; i < outSamples; i++)
            {
                var x0 = br.ReadInt16();
                var x1 = br.ReadInt16();
                input.Position -= 4;
                var v = Math.Round(x0 + (x1 - x0) * mu);
                v = Math.Max(short.MinValue, Math.Min(v, short.MaxValue));
                bw.Write((short)v);
                mu += rate;
                input.Position += (int)mu;
                mu -= (int)mu;
            }
        }

        public unsafe void AppendOgg(Stream input, double start, double end)
        {
            var streamOutput = new MemoryStream();
            var bw = new BinaryWriter(streamOutput);

            using (var vorbis = new VorbisReader(input))
            {
                var header = GetOggHeader(vorbis);
                streamOutput.Position = sizeof(WaveHeader);

                if (start != 0)
                    vorbis.SeekTo(TimeSpan.FromSeconds(start));

                // Stream samples from ogg
                var maxSamplesToRead = double.IsNaN(end) ? int.MaxValue : (int)(vorbis.Channels * vorbis.SampleRate * end);

                int readSamples;
                var readBuffer = new float[vorbis.Channels * vorbis.SampleRate / 8];
                while ((readSamples = vorbis.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    var leftToRead = Math.Min(readSamples, maxSamplesToRead);
                    if (Volume == 1)
                    {
                        for (int i = 0; i < leftToRead; i++)
                        {
                            var value = (short)(readBuffer[i] * short.MaxValue);
                            bw.Write(value);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < leftToRead; i++)
                        {
                            var value = (short)(readBuffer[i] * short.MaxValue * Volume);
                            bw.Write(value);
                        }
                    }
                    maxSamplesToRead -= leftToRead;
                }

                header.nRiffLength = (uint)(streamOutput.Position - 8);
                header.nDataLength = (uint)(streamOutput.Position - sizeof(WaveHeader));

                streamOutput.Position = 0;
                bw.Write(header);
            }

            streamOutput.Position = 0;
            AppendWav(streamOutput, 0, double.NaN);
        }

        private static WaveHeader GetOggHeader(VorbisReader vorbis)
        {
            var header = new WaveHeader();
            header.nRiffMagic = g_riffMagic;
            header.nRiffLength = 0;
            header.nWaveMagic = g_waveMagic;
            header.nFormatMagic = g_fmtMagic;
            header.nFormatLength = 16;
            header.wFormatTag = 1;
            header.nChannels = (ushort)vorbis.Channels;
            header.nSamplesPerSec = (uint)vorbis.SampleRate;
            header.nAvgBytesPerSec = (uint)(vorbis.SampleRate * 16 * vorbis.Channels) / 8;
            header.nBlockAlign = (ushort)((16 * vorbis.Channels) / 8);
            header.wBitsPerSample = 16;
            header.wDataMagic = g_dataMagic;
            header.nDataLength = 0;
            return header;
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
