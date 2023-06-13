using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IntelOrca.Biohazard;

namespace emdui.Extensions
{
    internal static class TimFileExtensions
    {
        public static BitmapSource ToBitmap(this TimFile timFile)
        {
            var pixels = timFile.GetPixels((x, y) => x / 128);
            return BitmapSource.Create(timFile.Width, timFile.Height, 96, 96, PixelFormats.Bgra32, null, pixels, timFile.Width * 4);
        }

        public static void Save(this BitmapSource source, string path)
        {
            var bitmapEncoder = new PngBitmapEncoder();
            bitmapEncoder.Frames.Add(BitmapFrame.Create(source));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                bitmapEncoder.Save(fs);
            }
        }
    }
}
