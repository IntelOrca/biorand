namespace IntelOrca.Biohazard
{
    public class EmdFile : ModelFile
    {
        protected override int Md2ChunkIndex => 14;
        public override int NumPages => 2;

        public EmdFile(string path)
            : base(path)
        {
        }
    }
}
