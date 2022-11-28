using System.Drawing;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal static class RandoBgCreator
    {
        public static void Save(RandoConfig config, string modPath, BioVersion version)
        {
            try
            {
                if (version == BioVersion.Biohazard1)
                {
                    var src = Resources.title_bg_1;
                    var dataPath = Path.Combine(modPath, "Data");
                    CreateRaw(config, dataPath, "title.pix", src);
                    CreateBitmap(config, modPath, "type.png", src);
                }
                else
                {
                    var src = Resources.title_bg_2;
                    var dataPath = Path.Combine(modPath, "Common", "Data");
                    CreateBitmap(config, dataPath, "title_bg.png", src);
                    CreateBitmap(config, dataPath, "type00.png", src);
                }
            }
            catch
            {
            }
        }

        private static void CreateRaw(RandoConfig config, string modPath, string filename, byte[] src)
        {
            var destPath = Path.Combine(modPath, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            using (var titleBg = CreateImage(config, src))
            {
                using (var fs = new FileStream(destPath, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    for (int y = 0; y < 240; y++)
                    {
                        for (int x = 0; x < 320; x++)
                        {
                            if (x >= titleBg.Width || y >= titleBg.Height)
                            {
                                bw.Write((byte)0);
                                bw.Write((byte)0);
                            }
                            else
                            {
                                var c = titleBg.GetPixel(x, y);
                                var c4 = (ushort)((c.R / 8) | ((c.G / 8) << 5) | ((c.B / 8) << 10));
                                bw.Write(c4);
                            }
                        }
                    }
                }
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

                var font = new Font("Courier New", 16, GraphicsUnit.Pixel);

                var versionInfo = Program.CurrentVersionInfo;
                var versionSize = g.MeasureString(versionInfo, font);
                g.DrawString(versionInfo, font, Brushes.White, 0, 0);

                var seed = config.ToString();
                var seedSize = g.MeasureString(seed, font);
                g.DrawString(seed, font, Brushes.White, titleBg.Width - seedSize.Width + 5, 0);
            }
            return titleBg;
        }
    }
}
