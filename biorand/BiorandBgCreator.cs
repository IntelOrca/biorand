using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IntelOrca.Biohazard.BioRand
{
    internal class BiorandBgCreator : IBgCreator
    {
        public byte[] CreatePNG(RandoConfig config, byte[] pngBackground)
        {
            var ms = new MemoryStream();
            using (var titleBg = CreateImage(config, pngBackground))
            {
                titleBg.Save(ms, ImageFormat.Png);
            }
            return ms.ToArray();
        }

        public unsafe byte[] CreateARGB(RandoConfig config, byte[] pngBackground)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            using (var titleBg = CreateImage(config, pngBackground))
            {
                var bounds = new Rectangle(0, 0, titleBg.Width, titleBg.Height);
                var bitmapData = titleBg.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var src = (uint*)bitmapData.Scan0;
                    var padding = bitmapData.Stride - (titleBg.Width * 4);
                    for (int y = 0; y < titleBg.Height; y++)
                    {
                        for (int x = 0; x < titleBg.Width; x++)
                        {
                            bw.Write(*src++);
                        }
                        src += padding;
                    }
                }
                finally
                {
                    titleBg.UnlockBits(bitmapData);
                }
            }
            return ms.ToArray();
        }

        private static void CreateBitmap(RandoConfig config, string modPath, string filename, byte[] src)
        {
            var destPath = Path.Combine(modPath, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            using (var titleBg = CreateImage(config, src))
            {
                titleBg.Save(destPath);
            }
        }

        private static Bitmap CreateImage(RandoConfig config, byte[] src)
        {
            var titleBg = new Bitmap(320, 240);
            using (var g = Graphics.FromImage(titleBg))
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                var srcImage = new Bitmap(new MemoryStream(src));
                g.DrawImage(srcImage, 0, 0, titleBg.Width, titleBg.Height);

                var font = new Font("Courier New", 14, GraphicsUnit.Pixel);

                var versionInfo = Program.CurrentVersionInfo;
                var versionSize = g.MeasureString(versionInfo, font);
                g.DrawString(versionInfo, font, Brushes.White, 0, 0);

                var seed = config.ToString();
                var seedSize = g.MeasureString(seed, font);
                g.DrawString(seed, font, Brushes.White, titleBg.Width - seedSize.Width + 1, 0);
            }
            return titleBg;
        }
    }
}
