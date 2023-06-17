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

        private const int RE3_CHUNK_EDD = 0;
        private const int RE3_CHUNK_EMR = 1;
        private const int RE3_CHUNK_MD1 = 2;
        private const int RE3_CHUNK_TIM = 8;

        public PlwFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        public override int NumPages => 1;
        protected override int Md1ChunkIndex => RE2_CHUNK_MD1;
        protected override int Md2ChunkIndex => RE3_CHUNK_MD1;

        public override Edd GetEdd(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            var chunkIndex = Version == BioVersion.Biohazard2 ? RE2_CHUNK_EDD : RE3_CHUNK_EDD;
            return new Edd(GetChunk(chunkIndex));
        }

        public override void SetEdd(int index, Edd edd)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            var chunkIndex = Version == BioVersion.Biohazard2 ? RE2_CHUNK_EDD : RE3_CHUNK_EDD;
            SetChunk(chunkIndex, edd.GetBytes());
        }

        public override Emr GetEmr(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            var chunkIndex = Version == BioVersion.Biohazard2 ? RE2_CHUNK_EMR : RE3_CHUNK_EMR;
            return new Emr(GetChunk(chunkIndex));
        }

        public override void SetEmr(int index, Emr emr)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            var chunkIndex = Version == BioVersion.Biohazard2 ? RE2_CHUNK_EMR : RE3_CHUNK_EMR;
            SetChunk(chunkIndex, emr.GetBytes());
        }

        public TimFile Tim
        {
            get
            {
                var chunkIndex = Version == BioVersion.Biohazard2 ? RE2_CHUNK_TIM : RE3_CHUNK_TIM;
                var ms = new MemoryStream(GetChunk(chunkIndex));
                return new TimFile(ms);
            }
            set
            {
                var chunkIndex = Version == BioVersion.Biohazard2 ? RE2_CHUNK_TIM : RE3_CHUNK_TIM;
                var ms = new MemoryStream();
                value.Save(ms);
                SetChunk(chunkIndex, ms.ToArray());
            }
        }
    }
}
