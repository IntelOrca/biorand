namespace IntelOrca.Biohazard
{
    public interface IBgCreator
    {
        byte[] CreatePNG(RandoConfig config, byte[] pngBackground);
        byte[] CreateARGB(RandoConfig config, byte[] pngBackground);
    }
}
