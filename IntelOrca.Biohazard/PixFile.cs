using System.IO;

namespace IntelOrca.Biohazard
{
    public class PixFile
    {
        private readonly byte[] _imageData;

        public int Width { get; }
        public int Height { get; }

        public PixFile(string path, int width, int height)
        {
            Width = width;
            Height= height;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                _imageData = new byte[width * height * 2];
                fs.Read(_imageData, 0, width * height * 2);
            }
        }

        public PixFile(Stream stream, int width, int height)
        {
            Width = width;
            Height = height;
            _imageData = new byte[width * height * 2];
            stream.Read(_imageData, 0, width * height * 2);
        }

        public uint[] GetPixels()
        {
            var result = new uint[Width * Height];
            var index = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = GetPixel(x, y);
                    result[index] = p;
                    index++;
                }
            }
            return result;
        }

        public uint GetPixel(int x, int y)
        {
            var offset = (y * Width * 2) + (x * 2);
            var p0 = _imageData[offset + 0];
            var p1 = _imageData[offset + 1];
            var c16 = (ushort)(p0 | (p1 << 8));
            return Convert16to32(c16);
        }

        public static uint Convert16to32(ushort c16)
        {
            // 0BBB_BBGG_GGGR_RRRR
            var r = ((c16 >> 0) & 0b11111) * 8;
            var g = ((c16 >> 5) & 0b11111) * 8;
            var b = ((c16 >> 10) & 0b11111) * 8;
            return (uint)(b | (g << 8) | (r << 16) | (255 << 24));
        }
    }
}
