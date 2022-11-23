using System.Drawing;
using System.IO;

namespace IntelOrca.Biohazard
{
    internal static class RandoBgCreator
    {
        public static void Save(RandoConfig config, string modPath)
        {
            try
            {
                CreateImage(config, modPath, "title_bg.png");
                CreateImage(config, modPath, "type00.png");
            }
            catch
            {
            }
        }

        private static void CreateImage(RandoConfig config, string modPath, string filename)
        {
            var destPath = Path.Combine(modPath, "Common", "Data", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            using (var titleBg = new Bitmap(320, 240))
            {
                using (var g = Graphics.FromImage(titleBg))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    var srcImage = new Bitmap(new MemoryStream(Resources.title_bg));
                    g.DrawImage(srcImage, 0, 0, titleBg.Width, titleBg.Height);

                    var font = new Font(FontFamily.GenericSansSerif, 6);

                    var versionInfo = Program.CurrentVersionInfo;
                    var versionSize = g.MeasureString(versionInfo, font);
                    g.DrawString(versionInfo, font, Brushes.White, 0, 0);

                    var seed = config.ToString();
                    var seedSize = g.MeasureString(seed, font);
                    g.DrawString(seed, font, Brushes.White, titleBg.Width - seedSize.Width + 5, 0);
                }
                titleBg.Save(destPath);
            }
        }
    }
}
