using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace IntelOrca.Biohazard.BioRand
{
    internal static class TimFileExtensions
    {
        public static Bitmap ToBitmap(this TimFile timFile)
        {
            return ToBitmap(timFile, (x, y) => 0);
        }

        public static unsafe Bitmap ToBitmap(this TimFile timFile, Func<int, int, int> getClutIndex)
        {
            var pixels = timFile.GetPixels(getClutIndex);
            var bitmap = new Bitmap(timFile.Width, timFile.Height);
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

        public static void ImportBitmap(this TimFile timFile, Bitmap bitmap)
        {
            var pixels = bitmap.ToArgb();
            timFile.ImportPixels(pixels, 0);
        }

        public static void ImportBitmap(this TimFile timFile, Bitmap bitmap, int x, int y, int clutIndex)
        {
            var pixels = bitmap.ToArgb();
            timFile.ImportPixels(x, y, bitmap.Width, bitmap.Height, pixels, clutIndex);
        }

        public static void ImportBitmap(this TimFile timFile, Bitmap bitmap, Rectangle srcBounds, int x, int y, int clutIndex)
        {
            var pixels = bitmap.ToArgb(srcBounds);
            timFile.ImportPixels(x, y, srcBounds.Width, srcBounds.Height, pixels, clutIndex);
        }
    }
}
