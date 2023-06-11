using System.IO;

namespace IntelOrca.Biohazard
{
    public class PldFile : ModelFile
    {
        private const int RE2_CHUNK_EDD = 0;
        private const int RE2_CHUNK_EMR = 1;
        private const int RE2_CHUNK_MD1 = 2;
        private const int RE2_CHUNK_TIM = 3;

        private const int RE3_CHUNK_MD2 = 2;
        private const int RE3_CHUNK_TIM = 4;

        protected override int Md1ChunkIndex => RE2_CHUNK_MD1;
        protected override int Md2ChunkIndex => RE3_CHUNK_MD2;
        private int TimChunkIndex => Version == BioVersion.Biohazard2 ? RE2_CHUNK_TIM : RE3_CHUNK_TIM;
        public override int NumPages => 3;

        public PldFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        public TimFile GetTim()
        {
            var meshChunk = GetChunk(TimChunkIndex);
            var ms = new MemoryStream(meshChunk);
            return new TimFile(ms);
        }

        public void SetTim(TimFile value)
        {
            var ms = new MemoryStream();
            value.Save(ms);
            SetChunk(TimChunkIndex, ms.ToArray());
        }
    }
}
