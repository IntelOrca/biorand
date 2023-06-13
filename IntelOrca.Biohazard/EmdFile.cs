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

        public override Emr GetEmr(int index)
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException(nameof(index));

            var offset = 2;
            if (Version == BioVersion.Biohazard3)
                offset = 3;

            return new Emr(GetChunk(offset + (index * 2)));
        }
    }
}
