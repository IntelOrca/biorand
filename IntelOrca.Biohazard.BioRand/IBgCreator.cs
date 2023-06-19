namespace IntelOrca.Biohazard
{
    public interface IBgCreator
    {
        byte[] CreatePNG(RandoConfig config, byte[] pngBackground);
        uint[] CreateARGB(RandoConfig config, byte[] pngBackground);
        void DrawImage(TimFile timFile, string srcImagePath, int x, int y);
        void DrawImage(TimFile timFile, string srcImagePath, int x, int y, int clutIndex);
    }
}
