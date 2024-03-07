using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QRCoder;

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

        public unsafe uint[] CreateARGB(RandoConfig config, byte[] pngBackground)
        {
            using (var titleBg = CreateImage(config, pngBackground))
            {
                return titleBg.ToArgb();
            }
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
            using (var srcImage = new Bitmap(new MemoryStream(src)))
            {
                // HACK
                var pictureRight = srcImage.Width == 1024 ? 640 : srcImage.Width;
                var size = srcImage.Width == 1024 ? 2 : 1;

                var titleBg = new Bitmap(srcImage.Width, srcImage.Height);
                using (var g = Graphics.FromImage(titleBg))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    g.DrawImage(srcImage, 0, 0, titleBg.Width, titleBg.Height);

                    var font = new Font("Courier New", 14 * size, GraphicsUnit.Pixel);

                    var versionInfo = Program.CurrentVersionInfo;
                    var versionSize = g.MeasureString(versionInfo, font);
                    g.DrawString(versionInfo, font, Brushes.White, 0, 0);

                    var seed = config.ToString();
                    // var seedSize = g.MeasureString(seed, font);
                    // g.DrawString(seed, font, Brushes.White, titleBg.Width - seedSize.Width + 1, 0);
                    var qr = GetQRImage(seed, size);
                    g.DrawImage(qr, pictureRight - qr.Width, 0);
                }
                return titleBg;
            }
        }

        public void DrawImage(TimFile timFile, string srcImagePath, int x, int y)
        {
            using (var srcBitmap = new Bitmap(srcImagePath))
            {
                using (var bitmap = timFile.ToBitmap())
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.DrawImage(srcBitmap, x, y, srcBitmap.Width, srcBitmap.Height);
                    }
                    timFile.ImportBitmap(bitmap);
                }
            }
        }

        public void DrawImage(TimFile timFile, string srcImagePath, int x, int y, int clutIndex)
        {
            using (var srcBitmap = new Bitmap(srcImagePath))
            {
                timFile.ImportBitmap(srcBitmap, x, y, clutIndex);
            }
        }

        private static Bitmap GetQRImage(string text, int size)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var qrCode = new QRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(size, Color.White, Color.Transparent, true);
            return qrCodeImage;
        }
    }
}
