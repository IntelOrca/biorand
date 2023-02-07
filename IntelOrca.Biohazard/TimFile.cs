using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard
{
    public class TimCollectionFile
    {
        public List<TimFile> Tims { get; } = new List<TimFile>();

        public TimCollectionFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            while (fs.Position < fs.Length)
            {
                var tim = new TimFile(fs);
                Tims.Add(tim);
            }
        }

        public void Save(string path)
        {
            using var fs = new FileStream(path, FileMode.Create);
            foreach (var tim in Tims)
            {
                tim.Save(fs);
            }
        }
    }

    public class TimFile
    {
        private const int PaletteFormat4bpp = 8;
        private const int PaletteFormat8bpp = 9;

        private uint _magic;
        private uint _pixelFormat;
        private uint _clutSize;
        private ushort _paletteOrgX;
        private ushort _paletteOrgY;
        private ushort _coloursPerClut;
        private ushort _numCluts;
        private byte[] _clutData = new byte[0];
        private uint _imageSize;
        private ushort _imageOrgX;
        private ushort _imageOrgY;
        private ushort _imageWidth;
        private ushort _imageHeight;
        private byte[] _imageData = new byte[0];

        public int Width => _pixelFormat == PaletteFormat4bpp ? _imageWidth * 4 : _imageWidth * 2;
        public int Height => _imageHeight;

        public TimFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            Read(fs);
        }

        public TimFile(Stream stream)
        {
            Read(stream);
        }

        private void Read(Stream stream)
        {
            var br = new BinaryReader(stream);
            _magic = br.ReadUInt32();
            if (_magic != 16)
            {
                throw new Exception("Invalid TIM file");
            }

            _pixelFormat = br.ReadUInt32();
            if (_pixelFormat != PaletteFormat4bpp && _pixelFormat != PaletteFormat8bpp)
            {
                throw new NotSupportedException("Unsupported TIM pixel format");
            }

            _clutSize = br.ReadUInt32();
            _paletteOrgX = br.ReadUInt16();
            _paletteOrgY = br.ReadUInt16();
            _coloursPerClut = br.ReadUInt16();
            _numCluts = br.ReadUInt16();

            var expectedClutDataSize = _numCluts * _coloursPerClut * 2;
            _clutData = br.ReadBytes((int)_clutSize - 12);

            _imageSize = br.ReadUInt32();
            _imageOrgX = br.ReadUInt16();
            _imageOrgY = br.ReadUInt16();
            _imageWidth = br.ReadUInt16();
            _imageHeight = br.ReadUInt16();

            var expectedImageDataSize = Width * Height;
            _imageData = br.ReadBytes((int)_imageSize - 12);
        }

        public void Save(string path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            Save(fs);
        }

        public void Save(Stream stream)
        {
            var bw = new BinaryWriter(stream);
            bw.Write(_magic);
            bw.Write(_pixelFormat);
            bw.Write(_clutSize);
            bw.Write(_paletteOrgX);
            bw.Write(_paletteOrgY);
            bw.Write(_coloursPerClut);
            bw.Write(_numCluts);
            bw.Write(_clutData);
            bw.Write(_imageSize);
            bw.Write(_imageOrgX);
            bw.Write(_imageOrgY);
            bw.Write(_imageWidth);
            bw.Write(_imageHeight);
            bw.Write(_imageData);
        }

        private ushort GetCLUTEntry(int clutIndex, int index)
        {
            var clutSize = _coloursPerClut * 2;
            var clutOffset = (clutIndex * clutSize) + (index * 2);
            var c16 = (ushort)(_clutData[clutOffset] + (_clutData[clutOffset + 1] << 8));
            return c16;
        }

        public uint GetARGB(int clutIndex, int index)
        {
            // 0BBB_BBGG_GGGR_RRRR
            var c16 = GetCLUTEntry(clutIndex, index);
            var r = ((c16 >> 0) & 0b11111) * 8;
            var g = ((c16 >> 5) & 0b11111) * 8;
            var b = ((c16 >> 10) & 0b11111) * 8;
            return (uint)(b | (g << 8) | (r << 16) | (255 << 24));
        }

        public uint GetPixel(int x, int y)
        {
            byte p;
            if (_pixelFormat == PaletteFormat4bpp)
            {
                var offset = (y * _imageWidth * 2) + (x / 2);
                var b = _imageData[offset];
                p = (byte)((x & 1) == 0 ? b & 0x0F : b >> 4);
            }
            else
            {
                var offset = (y * Width) + x;
                p = _imageData[offset];
            }
            return GetARGB(0, p);
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

        private byte ImportPixel(uint p)
        {
            var r = (byte)((p >> 16) & 0xFF);
            var g = (byte)((p >> 8) & 0xFF);
            var b = (byte)((p >> 0) & 0xFF);

            var bestIndex = -1;
            var bestTotal = int.MaxValue;
            for (int i = 0; i < _coloursPerClut; i++)
            {
                var entry = GetARGB(0, i);
                var entryR = (byte)((entry >> 16) & 0xFF);
                var entryG = (byte)((entry >> 8) & 0xFF);
                var entryB = (byte)((entry >> 0) & 0xFF);
                var deltaR = Math.Abs(entryR - r);
                var deltaG = Math.Abs(entryG - g);
                var deltaB = Math.Abs(entryB - b);
                var total = deltaR + deltaG + deltaB;
                if (total < bestTotal)
                {
                    bestIndex = i;
                    bestTotal = total;
                }
            }
            return (byte)bestIndex;
        }

        public void ImportPixels(uint[] data)
        {
            var index = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    _imageData[index] = ImportPixel(data[index]);
                    index++;
                }
            }
        }
    }
}
