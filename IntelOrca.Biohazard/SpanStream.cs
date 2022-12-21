using System;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class SpanStream : Stream
    {
        private readonly ReadOnlyMemory<byte> _memory;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _memory.Length;
        public override long Position
        {
            get => _position;
            set
            {
                if (value > Length)
                    throw new InvalidOperationException("Position past end of stream.");
                _position = value;
            }
        }

        public SpanStream(ReadOnlyMemory<byte> memory)
        {
            _memory = memory;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = Length - Position;
            var read = (int)Math.Min(remaining, count);
            if (read == 0)
                return 0;

            var src = _memory.Span.Slice((int)_position, count);
            var dst = new Span<byte>(buffer, offset, read);
            src.CopyTo(dst);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
                Position = offset;
            else if (origin == SeekOrigin.Current)
                Position += offset;
            else if (origin == SeekOrigin.End)
                Position = Length - offset;
            else
                throw new ArgumentException("Invalid origin type.", nameof(origin));
            return Position;
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}
