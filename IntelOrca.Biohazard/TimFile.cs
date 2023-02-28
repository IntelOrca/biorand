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
        private const int PaletteFormat16bpp = 2;

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

        public int Width => _pixelFormat switch
        {
            PaletteFormat4bpp => _imageWidth * 4,
            PaletteFormat8bpp => _imageWidth * 2,
            PaletteFormat16bpp => _imageWidth,
            _ => throw new NotSupportedException(),
        };

        public int Height => _imageHeight;

        public TimFile(int width, int height, int bpp)
        {
            if (bpp == 8)
            {
                _magic = 16;
                _pixelFormat = PaletteFormat8bpp;
                _imageSize = ((uint)width * (uint)height) + 12;
                _imageOrgX = 0;
                _imageOrgY = 0;
                _imageWidth = (ushort)(width / 2);
                _imageHeight = (ushort)height;
                _imageData = new byte[width * height];

                _paletteOrgX = 0;
                _paletteOrgY = 0;
                _coloursPerClut = 256;
                ResizeCluts(1);

                // _numCluts = 3;
                // _clutSize = (uint)(_numCluts * _coloursPerClut * 2) + 12;
                // _clutData = new byte[_clutSize - 12];
                // for (int j = 0; j < _numCluts; j++)
                // {
                //     for (int i = 0; i < 256; i++)
                //     {
                //         var r = ((i & 0b00_000_111) >> 0) * 32;
                //         var g = ((i & 0b00_111_000) >> 3) * 32;
                //         var b = ((i & 0b11_000_000) >> 6) * 64;
                //         var argb = (uint)((r << 16) | (g << 8) | b);
                //         var c16 = Convert32to16(argb);
                //         _clutData[(j * 512) + (i * 2)] = (byte)(c16 & 0xFF);
                //         _clutData[(j * 512) + (i * 2) + 1] = (byte)(c16 >> 8);
                //     }
                // }
            }
            else if (bpp == 16)
            {
                _magic = 16;
                _pixelFormat = PaletteFormat16bpp;
                _imageSize = ((uint)width * (uint)height * 2) + 12;
                _imageOrgX = 0;
                _imageOrgY = 0;
                _imageWidth = (ushort)width;
                _imageHeight = (ushort)height;
                _imageData = new byte[width * height * 2];
            }
            else
            {
                throw new ArgumentException("Bits per pixel must be 8 or 16", nameof(bpp));
            }
        }

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
            if (_pixelFormat != PaletteFormat4bpp &&
                _pixelFormat != PaletteFormat8bpp &&
                _pixelFormat != PaletteFormat16bpp)
            {
                throw new NotSupportedException("Unsupported TIM pixel format");
            }

            if (_pixelFormat != PaletteFormat16bpp)
            {
                _clutSize = br.ReadUInt32();
                _paletteOrgX = br.ReadUInt16();
                _paletteOrgY = br.ReadUInt16();
                _coloursPerClut = br.ReadUInt16();
                _numCluts = br.ReadUInt16();

                var expectedClutDataSize = _numCluts * _coloursPerClut * 2;
                _clutData = br.ReadBytes((int)_clutSize - 12);
            }

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
            if (_pixelFormat != PaletteFormat16bpp)
            {
                bw.Write(_clutSize);
                bw.Write(_paletteOrgX);
                bw.Write(_paletteOrgY);
                bw.Write(_coloursPerClut);
                bw.Write(_numCluts);
                bw.Write(_clutData);
            }
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
            var c16 = GetCLUTEntry(clutIndex, index);
            return Convert16to32(c16);
        }

        public uint GetPixel(int x, int y, int clutIndex = 0)
        {
            switch (_pixelFormat)
            {
                case PaletteFormat4bpp:
                    {
                        var offset = (y * _imageWidth * 2) + (x / 2);
                        if (offset >= _imageData.Length)
                            return 0;
                        var b = _imageData[offset];
                        var p = (byte)((x & 1) == 0 ? b & 0x0F : b >> 4);
                        return GetARGB(clutIndex, p);
                    }
                case PaletteFormat8bpp:
                    {
                        var offset = (y * Width) + x;
                        var p = _imageData.Length > offset ? _imageData[offset] : 0;
                        return GetARGB(clutIndex, p);
                    }
                case PaletteFormat16bpp:
                    {
                        var offset = (y * Width * 2) + (x * 2);
                        if (offset + 1 >= _imageData.Length)
                            return 0;

                        var p0 = _imageData[offset + 0];
                        var p1 = _imageData[offset + 1];
                        var c16 = (ushort)(p0 | (p1 << 8));
                        return Convert16to32(c16);
                    }
                default:
                    throw new InvalidOperationException();
            }
        }

        public void SetPixel(int x, int y, int clutIndex, uint p)
        {
            switch (_pixelFormat)
            {
                case PaletteFormat4bpp:
                    {
                        var offset = (y * _imageWidth * 2) + (x / 2);
                        var c8 = ImportPixel(clutIndex, p);
                        var b = _imageData[offset];
                        if ((x & 1) == 0)
                            b = (byte)((b & 0xF0) | c8);
                        else
                            b = (byte)((b & 0x0F) | (c8 << 4));
                        _imageData[offset] = b;
                        break;
                    }
                case PaletteFormat8bpp:
                    {
                        var offset = (y * Width) + x;
                        var c8 = ImportPixel(clutIndex, p);
                        _imageData[offset] = c8;
                        break;
                    }
                case PaletteFormat16bpp:
                    {
                        var offset = (y * Width * 2) + (x * 2);
                        var c16 = Convert32to16(p);
                        _imageData[offset + 0] = (byte)(c16 & 0xFF);
                        _imageData[offset + 1] = (byte)(c16 >> 8);
                        break;
                    }
                default:
                    throw new InvalidOperationException();
            }
        }

        public uint[] GetPixels() => GetPixels((x, y) => 0);

        public uint[] GetPixels(Func<int, int, int> getClutIndex)
        {
            var result = new uint[Width * Height];
            var index = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = GetPixel(x, y, getClutIndex(x, y));
                    result[index] = p;
                    index++;
                }
            }
            return result;
        }

        private byte ImportPixel(int clutIndex, uint p)
        {
            var r = (byte)((p >> 16) & 0xFF);
            var g = (byte)((p >> 8) & 0xFF);
            var b = (byte)((p >> 0) & 0xFF);

            var bestIndex = -1;
            var bestTotal = int.MaxValue;
            for (int i = 0; i < _coloursPerClut; i++)
            {
                var entry = GetARGB(clutIndex, i);
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

        public void ImportPixels(uint[] data, int clutIndex)
        {
            var index = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    SetPixel(x, y, clutIndex, data[index]);
                    index++;
                }
            }
        }

        public unsafe void ImportPixels(int x, int y, int width, int height, uint[] data, int clutIndex)
        {
            var index = 0;
            for (int yy = y; yy < y + height; yy++)
            {
                for (int xx = x; xx < x + width; xx++)
                {
                    SetPixel(xx, yy, clutIndex, data[index]);
                    index++;
                }
            }
        }

        public void SetPalette(int clutIndex, ushort[] colours)
        {
            for (int i = 0; i < colours.Length; i++)
            {
                SetPalette(clutIndex, i, colours[i]);
            }
        }

        public void SetPalette(int clutIndex, int index, ushort c16)
        {
            if (_numCluts <= clutIndex)
            {
                ResizeCluts(clutIndex + 1);
            }

            var offset = (clutIndex * _coloursPerClut * 2) + (index * 2);
            _clutData[offset + 0] = (byte)(c16 & 0xFF);
            _clutData[offset + 1] = (byte)(c16 >> 8);
        }

        private void ResizeCluts(int count)
        {
            _clutSize = (uint)((count * _coloursPerClut * 2) + 12);
            Array.Resize(ref _clutData, (int)(_clutSize - 12));
        }

        public static uint Convert16to32(ushort c16)
        {
            // 0BBB_BBGG_GGGR_RRRR
            var r = ((c16 >> 0) & 0b11111) * 8;
            var g = ((c16 >> 5) & 0b11111) * 8;
            var b = ((c16 >> 10) & 0b11111) * 8;
            return (uint)(b | (g << 8) | (r << 16) | (255 << 24));
        }

        public static ushort Convert32to16(uint c32)
        {
            var r = (byte)(((c32 >> 16) & 0xFF) / 8);
            var g = (byte)(((c32 >> 8) & 0xFF) / 8);
            var b = (byte)(((c32 >> 0) & 0xFF) / 8);

            // 0BBB_BBGG_GGGR_RRRR
            return (ushort)((b << 10) | (g << 5) | r);
        }
    }
}
