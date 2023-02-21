using System.Drawing;
using System.Drawing.Imaging;

namespace IntelOrca.Biohazard.BioRand
{
    internal static class PixFileExtensions
    {
        public static unsafe Bitmap ToBitmap(this PixFile pixFile)
        {
            var pixels = pixFile.GetPixels();
            var bitmap = new Bitmap(pixFile.Width, pixFile.Height);
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var dst = (uint*)bitmapData.Scan0;
                var padding = bitmapData.Stride - (bitmap.Width * 4);
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        *dst++ = pixels[y * bitmap.Width + x];
                    }
                    dst += padding;
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
            return bitmap;
        }
    }
}
