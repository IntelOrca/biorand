using System;

namespace IntelOrca.Biohazard
{
    public class EmdFile : ModelFile
    {
        protected override int Md1ChunkIndex => 7;
        protected override int Md2ChunkIndex => 14;
        public override int NumPages => 2;

        public EmdFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        private int GetEmrChunkIndex(int index)
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException(nameof(index));

            var offset = 2;
            if (Version == BioVersion.Biohazard3)
                offset = 3;

            return offset + (index * 2);
        }

        public override Emr GetEmr(int index)
        {
            return new Emr(GetChunk(GetEmrChunkIndex(index)));
        }

        public override void SetEmr(int index, Emr emr)
        {
            SetChunk(GetEmrChunkIndex(index), emr.GetBytes());
        }
    }
}
