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

        public WaveformBuilder()
        {
        }

        public WaveformBuilder(string templatePath)
        {
            Append(templatePath, 0, 0);
        }

        private void WriteHeader(in WaveHeader header)
        {
            var bw = new BinaryWriter(_stream);
            bw.Write(header);
            _header = header;
            _headerWritten = true;
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
            Finish();
            using (var fs = new FileStream(path, FileMode.Create))
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
                throw new InvalidOperationException("No previous waveform appended yet.");

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

        public void Append(string path, double start, double end)
        {
            if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);
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
                    fs.Position += startOffset;
                    fs.CopyAmountTo(_stream, length);
                }
            }
        }

        private static int AlignTime(in WaveHeader header, double t)
        {
            var precise = header.nAvgBytesPerSec * t;
            return ((int)precise / header.nBlockAlign) * header.nBlockAlign;
        }
    }
}
