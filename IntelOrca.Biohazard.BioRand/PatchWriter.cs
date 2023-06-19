using System;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class PatchWriter
    {
        private readonly Stream _stream;
        private readonly BinaryWriter _bw;
        private long? _patchBegin;

        public PatchWriter(Stream stream)
        {
            _stream = stream;
            _bw = new BinaryWriter(stream);
        }

        public void Begin(uint offset)
        {
            if (_patchBegin != null)
                throw new InvalidOperationException("Patch already in progress");

            _patchBegin = _stream.Position;
            _bw.Write(offset);
            _bw.Write(0);
        }

        public void Write(byte b) => _bw.Write(b);

        public void End()
        {
            if (_patchBegin == null)
                throw new InvalidOperationException("Patch not in progress");

            var position = _stream.Position;
            var length = (uint)(position - _patchBegin.Value - 8);
            _stream.Position = _patchBegin.Value + 4;
            _bw.Write(length);
            _stream.Position = position;
            _patchBegin = null;
        }
    }
}
