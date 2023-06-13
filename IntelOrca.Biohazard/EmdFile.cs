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

        public Emr GetEmr(int index)
        {
            return new Emr(GetChunk(2 + (index * 2)));
        }
    }
}
