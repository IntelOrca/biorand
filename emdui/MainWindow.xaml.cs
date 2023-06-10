using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IntelOrca.Biohazard;
using Microsoft.Win32;

namespace emdui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _path;
        private EmdFile _emdFile;
        private TimFile _timFile;

        private Md1 _md1;
        private BitmapSource _timImage;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadEmd(string path)
        {
            _path = path;
            _emdFile = new EmdFile(BioVersion.Biohazard2, _path);

            _md1 = _emdFile.Md1;
            listObjects.ItemsSource = Enumerable.Range(0, _md1.NumObjects / 2)
                .Select(x => $"Object {x}")
                .ToArray();
        }

        private void LoadTim(string path)
        {
            _path = path;
            _timFile = new TimFile(_path);
            RefreshTimImage();
        }

        private void RefreshTimImage()
        {
            var pixels = _timFile.GetPixels((x, y) => x / 128);
            timImage.Width = _timFile.Width;
            timImage.Height = _timFile.Height;
            timImage.Source = _timImage = BitmapSource.Create(_timFile.Width, _timFile.Height, 96, 96, PixelFormats.Bgra32, null, pixels, _timFile.Width * 4);
        }

        private void timImage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTim(@"M:\git\rer\IntelOrca.Biohazard\data\re2\emd\wesker\em050.tim");
            LoadEmd(@"M:\git\rer\IntelOrca.Biohazard\data\re2\emd\wesker\em050.emd");
        }

        private void menuOpen_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            openFileDialog.Filter = "TIM Files (*.tim)|*.tim";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadTim(openFileDialog.FileName);

                var emdPath = Path.ChangeExtension(openFileDialog.FileName, ".emd");
                if (File.Exists(emdPath))
                {
                    LoadEmd(emdPath);
                }
            }
        }

        private void menuSave_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            saveFileDialog.Filter = "TIM Files (*.tim)|*.tim";
            if (saveFileDialog.ShowDialog() == true)
            {
                _timFile.Save(saveFileDialog.FileName);
            }
        }

        private void menuImportTim_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            openFileDialog.Filter = "PNG (*.png)|*.png";
            if (openFileDialog.ShowDialog() == true)
            {
                _timFile = ImportTimFile16(openFileDialog.FileName);
                RefreshTimImage();
            }
        }

        private TimFile ImportTimFile16(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var bitmapDecoder = new PngBitmapDecoder(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = bitmapDecoder.Frames[0];
                var convertedFrame = new FormatConvertedBitmap(frame, PixelFormats.Bgr32, null, 0);

                // var timFile = new TimFile(convertedFrame.PixelWidth, convertedFrame.PixelHeight, 16);
                var timFile = _timFile;
                var pixels = new uint[timFile.Width * timFile.Height];
                convertedFrame.CopyPixels(pixels, timFile.Width * 4, 0);
                timFile.ImportPixels(pixels, (x, y) => x / 128);
                return timFile;
            }
        }

        private void menuExportTim_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            saveFileDialog.Filter = "PNG (*.png)|*.png";
            if (saveFileDialog.ShowDialog() == true)
            {
                var bitmapEncoder = new PngBitmapEncoder();
                bitmapEncoder.Frames.Add(BitmapFrame.Create(_timImage));
                using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write))
                {
                    bitmapEncoder.Save(fs);
                }
            }
        }

        private void listObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedIndex = listObjects.SelectedIndex * 2;
            if (selectedIndex >= 0 && selectedIndex < _md1.NumObjects)
            {
                var objTri = _md1.Objects[selectedIndex + 0];
                var objQuad = _md1.Objects[selectedIndex + 1];
                var triangles = _md1.GetTriangles(objTri);
                var quads = _md1.GetQuads(objQuad);

                var items = new List<string>();
                for (int i = 0; i < triangles.Length; i++)
                    items.Add($"Triangle {i}");
                for (int i = 0; i < quads.Length; i++)
                    items.Add($"Quad {i}");

                listPrimitives.ItemsSource = items;
            }
        }

        private void listPrimitives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var objIndex = listObjects.SelectedIndex * 2;
            if (objIndex >= 0 && objIndex < _md1.NumObjects)
            {
                var objTri = _md1.Objects[objIndex + 0];
                var objQuad = _md1.Objects[objIndex + 1];
                var triangles = _md1.GetTriangles(objTri);
                var triangleTextures = _md1.GetTriangleTextures(objTri);
                var quads = _md1.GetQuads(objQuad);
                var quadTextures = _md1.GetQuadTextures(objQuad);

                var priIndex = listPrimitives.SelectedIndex;
                if (priIndex >= 0 && priIndex < triangles.Length)
                {
                    var tri = triangles[priIndex];
                    var triTex = triangleTextures[priIndex];
                    textPrimitive.Text = string.Join("\n", new[] {
                        "u0 = " + triTex.u0,
                        "v0 = " + triTex.v0,
                        "clutId = " + triTex.clutId,
                        "u1 = " + triTex.u1,
                        "v1 = " + triTex.v1,
                        "page = " + triTex.page,
                        "u2 = " + triTex.u2,
                        "v2 = " + triTex.v2,
                        "zero = " + triTex.zero
                    });
                }
                else if (priIndex >= triangles.Length && priIndex < triangles.Length + quads.Length)
                {
                    priIndex -= triangles.Length;
                    var quad = quads[priIndex];
                    textPrimitive.Text = string.Join("\n", new[] {
                        quad.n0,
                        quad.v0,
                        quad.n1,
                        quad.v1,
                        quad.n2,
                        quad.v2,
                        quad.n3,
                        quad.v3
                    });
                }
            }
        }
    }
}
