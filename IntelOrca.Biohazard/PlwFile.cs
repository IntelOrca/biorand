using System;
using System.IO;

namespace IntelOrca.Biohazard
{
    public class PlwFile : ModelFile
    {
        private const int RE2_CHUNK_EDD = 0;
        private const int RE2_CHUNK_EMR = 1;
        private const int RE2_CHUNK_MD1 = 2;
        private const int RE2_CHUNK_TIM = 3;

        public PlwFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        public override int NumPages => 1;
        protected override int Md1ChunkIndex => RE2_CHUNK_MD1;
        protected override int Md2ChunkIndex => 0;

        public override Emr GetEmr(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return new Emr(GetChunk(RE2_CHUNK_EMR));
        }

        public override void SetEmr(int index, Emr emr)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            SetChunk(RE2_CHUNK_EMR, emr.GetBytes());
        }

        public TimFile Tim
        {
            get
            {
                var ms = new MemoryStream(GetChunk(RE2_CHUNK_TIM));
                return new TimFile(ms);
            }
            set
            {
                var ms = new MemoryStream();
                value.Save(ms);
                SetChunk(RE2_CHUNK_TIM, ms.ToArray());
            }
        }
    }
}
