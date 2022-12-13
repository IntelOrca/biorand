using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace IntelOrca.Biohazard
{
    internal class WaveformBuilder
    {
        private MemoryStream _stream = new MemoryStream();
        private WaveHeader _header;
        private bool _headerWritten;
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

        public void Save(string path)
        {
            if (!_headerWritten)
                throw new InvalidOperationException();

            Finish();
            using (var fs = new FileStream(path, FileMode.Create))
            {
                if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                {
                    var bw = new BinaryWriter(fs);
                    bw.Write((ulong)1);
                }
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
