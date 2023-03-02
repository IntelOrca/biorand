using System.Drawing;
using System.Drawing.Imaging;

namespace IntelOrca.Biohazard.BioRand
{
    public static class BitmapExtensions
    {
        public static uint[] ToArgb(this Bitmap bitmap)
        {
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            return ToArgb(bitmap, bounds);
        }

        public static unsafe uint[] ToArgb(this Bitmap bitmap, Rectangle bounds)
        {
            var result = new uint[bounds.Width * bounds.Height];
            var bitmapData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var src = (uint*)bitmapData.Scan0;
                var padding = bitmapData.Stride - (bounds.Width * 4);

                var index = 0;
                for (int y = 0; y < bounds.Height; y++)
                {
                    for (int x = 0; x < bounds.Width; x++)
                    {
                        result[index++] = *src++;
                    }
                    src = (uint*)((byte*)src + padding);
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
            return result;
        }
    }
}
