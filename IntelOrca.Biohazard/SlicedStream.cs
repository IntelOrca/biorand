using System;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class SlicedStream : Stream
    {
        private readonly Stream _stream;
        private readonly long _begin;
        private readonly long _end;

        public SlicedStream(Stream stream, long begin, long length)
        {
            _stream = stream;
            _begin = begin;
            _end = begin + length;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _end - _begin;

        public override long Position
        {
            get => _stream.Position - _begin;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var left = _end - Position;
            if (count > left)
                count = (int)left;
            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _stream.Position = _begin + offset;
                    break;
                case SeekOrigin.Current:
                    var newPosition = _stream.Position - _begin + offset;
                    if (newPosition < 0 || newPosition > _end)
                        throw new InvalidOperationException("Can not seek past end of stream.");
                    _stream.Position = _begin + newPosition;
                    break;
                case SeekOrigin.End:
                    Seek(_end - offset, SeekOrigin.Current);
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin", nameof(origin));
            }
            return Position;
        }

        public override void Close() => _stream.Close();
    }
}
