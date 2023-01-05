using System;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal class ForkedBinaryReader : BinaryReader, IDisposable
    {
        private long _backupPosition;

        internal ForkedBinaryReader(BinaryReader br)
            : base(br.BaseStream)
        {
            _backupPosition = br.BaseStream.Position;
        }

        public new void Dispose()
        {
            BaseStream.Position = _backupPosition;
        }
    }
}
