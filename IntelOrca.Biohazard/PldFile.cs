using System.IO;

namespace IntelOrca.Biohazard
{
    public class PldFile : ModelFile
    {
        private const int CHUNK_MESH = 2;
        private const int CHUNK_TIM = 4;

        protected override int Md2ChunkIndex => CHUNK_MESH;
        public override int NumPages => 3;

        public PldFile(string path)
            : base(path)
        {
        }

        public TimFile GetTim()
        {
            var meshChunk = GetChunk(CHUNK_TIM);
            var ms = new MemoryStream(meshChunk);
            return new TimFile(ms);
        }

        public void SetTim(TimFile value)
        {
            var ms = new MemoryStream();
            value.Save(ms);
            SetChunk(CHUNK_TIM, ms.ToArray());
        }
    }
}
