using QRCoder;
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
                // var seedSize = g.MeasureString(seed, font);
                // g.DrawString(seed, font, Brushes.White, titleBg.Width - seedSize.Width + 1, 0);
                var qr = GetQRImage(seed);
                g.DrawImage(qr, titleBg.Width - qr.Width, 0);
            }
            return titleBg;
        }

        public void DrawImage(TimFile timFile, string srcImagePath, int x, int y)
        {
            using (var srcBitmap = new Bitmap(srcImagePath))
            {
                using (var bitmap = timFile.ToBitmap())
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.DrawImage(srcBitmap, x, y, 30, 30);
                    }
                    timFile.ImportBitmap(bitmap);
                }
            }
        }

        private static Bitmap GetQRImage(string text)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(1, Color.White, Color.Transparent, true);
            return qrCodeImage;
        }
    }
}
