using System.Drawing;
using System.Drawing.Imaging;

namespace IntelOrca.Biohazard.BioRand
{
    public static class BitmapExtensions
    {
        public static unsafe uint[] ToArgb(this Bitmap bitmap)
        {
            var result = new uint[bitmap.Width * bitmap.Height];
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var src = (uint*)bitmapData.Scan0;
                var padding = bitmapData.Stride - (bitmap.Width * 4);

                var index = 0;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
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
