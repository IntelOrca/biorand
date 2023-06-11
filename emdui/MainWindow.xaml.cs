using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
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
        private ModelFile _modelFile;
        private TimFile _timFile;

        private Md1 _md1;
        private BitmapSource _timImage;

        public MainWindow()
        {
            InitializeComponent();
            Work();
        }

        private void Work()
        {
        }

        private void LoadModel(string path)
        {
            _path = path;
            if (path.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
            {
                _modelFile = new EmdFile(BioVersion.Biohazard2, _path);
                if (path.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
                {
                    var timPath = Path.ChangeExtension(path, ".tim");
                    if (File.Exists(timPath))
                    {
                        LoadTim(timPath);
                    }
                }
            }
            else
            {
                var pldFile = new PldFile(BioVersion.Biohazard2, _path);
                _modelFile = pldFile;
                _timFile = pldFile.GetTim();
                RefreshTimImage();
            }

            _md1 = _modelFile.Md1;
            listObjects.ItemsSource = Enumerable.Range(0, _md1.NumObjects / 2)
                .Select(x => $"Object {x}")
                .ToArray();

            RefreshModelView();
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

        private void RefreshModelView()
        {
            // var camera = myViewport.Camera;
            var children = myViewport.Children;
            children.Clear();

            var selectedObject = listObjects.SelectedIndex;
            if (selectedObject < 0 || selectedObject >= _md1.NumObjects)
                return;

            // var textureWidth = (double)_timImage.PixelWidth;
            var textureWidth = (double)_timImage.PixelWidth;
            var textureHeight = (double)_timImage.PixelHeight;
            var mesh = new MeshGeometry3D();
            {
                var objTriangles = _md1.Objects[selectedObject * 2];
                var dataTriangles = _md1.GetTriangles(objTriangles);
                var dataTriangleTextures = _md1.GetTriangleTextures(objTriangles);
                var dataPositions = _md1.GetPositionData(objTriangles);
                var dataNormals = _md1.GetNormalData(objTriangles);
                for (var i = 0; i < dataTriangles.Length; i++)
                {
                    var triangle = dataTriangles[i];
                    var texture = dataTriangleTextures[i];

                    mesh.Positions.Add(dataPositions[triangle.v0].ToPoint3D());
                    mesh.Positions.Add(dataPositions[triangle.v1].ToPoint3D());
                    mesh.Positions.Add(dataPositions[triangle.v2].ToPoint3D());

                    mesh.Normals.Add(dataNormals[triangle.n0].ToVector3D());
                    mesh.Normals.Add(dataNormals[triangle.n1].ToVector3D());
                    mesh.Normals.Add(dataNormals[triangle.n2].ToVector3D());

                    var page = texture.page & 0x0F;
                    var offsetU = page * 128;
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u0) / textureWidth, (texture.v0 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u1) / textureWidth, (texture.v1 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u2) / textureWidth, (texture.v2 / textureHeight)));
                }
            }
            {
                var objQuads = _md1.Objects[(selectedObject * 2) + 1];
                var dataQuads = _md1.GetQuads(objQuads);
                var dataPositions = _md1.GetPositionData(objQuads);
                var dataNormals = _md1.GetNormalData(objQuads);
                var dataQuadTextures = _md1.GetQuadTextures(objQuads);
                for (var i = 0; i < dataQuads.Length; i++)
                {
                    var quad = dataQuads[i];
                    var texture = dataQuadTextures[i];
                    mesh.Positions.Add(dataPositions[quad.v0].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v1].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v2].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v3].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v2].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v1].ToPoint3D());

                    mesh.Normals.Add(dataNormals[quad.n0].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n1].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n2].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n0].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n2].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n3].ToVector3D());

                    var page = texture.page & 0x0F;
                    var offsetU = page * 128;
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u0) / textureWidth, (texture.v0 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u1) / textureWidth, (texture.v1 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u2) / textureWidth, (texture.v2 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u0) / textureWidth, (texture.v0 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u2) / textureWidth, (texture.v2 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u3) / textureWidth, (texture.v3 / textureHeight)));
                }
            }

            var model = new GeometryModel3D();
            model.Geometry = mesh;
            model.Material = new DiffuseMaterial(System.Windows.Media.Brushes.Red);
            ((DiffuseMaterial)model.Material).Brush = new ImageBrush(_timImage)
            {
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute
            };

            var modelVisual = new ModelVisual3D();
            modelVisual.Content = model;
            children.Add(modelVisual);

            // myViewport.Camera = new PerspectiveCamera(
            //     new Point3D(0, 0, 500),
            //     new Vector3D(),
            //     new Vector3D(0, 1, 0),
            //     70);

            var camera = new OrthographicCamera(
                new Point3D(-1000, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, -1, 0),
                5000);
            camera.FarPlaneDistance = 5000;
            camera.NearPlaneDistance = 1;
            // var camera = myViewport.Camera as PerspectiveCamera;
            // camera.Position = new Point3D(0, 0, -20);
            // camera.LookDirection = new Vector3D(0, 0, 1);
            myViewport.Camera = camera;

            myViewport.Children.Add(
                new ModelVisual3D() { Content = new AmbientLight(Colors.White) });
        }

        private void mainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadModel(@"M:\git\rer\IntelOrca.Biohazard\data\re2\pld1\alyssa\pl01.pld");
        }

        private void menuOpen_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            openFileDialog.Filter = "EMD/PLD Files (*.emd;*.pld)|*.emd;*.pld";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadModel(openFileDialog.FileName);
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
            RefreshModelView();
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
                    var positionData = _md1.GetPositionData(objTri);
                    var v0 = positionData[tri.v0];
                    var v1 = positionData[tri.v1];
                    var v2 = positionData[tri.v2];
                    textPrimitive.Text = string.Join("\n", new[] {
                        $"v0 = ({v0.x}, {v0.y}, {v0.z})",
                        $"v1 = ({v1.x}, {v1.y}, {v1.z})",
                        $"v2 = ({v2.x}, {v2.y}, {v2.z})"
                    });
                    // textPrimitive.Text = string.Join("\n", new[] {
                    //     "u0 = " + triTex.u0,
                    //     "v0 = " + triTex.v0,
                    //     "clutId = " + triTex.clutId,
                    //     "u1 = " + triTex.u1,
                    //     "v1 = " + triTex.v1,
                    //     "page = " + triTex.page,
                    //     "u2 = " + triTex.u2,
                    //     "v2 = " + triTex.v2,
                    //     "zero = " + triTex.zero
                    // });
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

    internal static class Md1Extensions
    {
        public static Point3D ToPoint3D(this Md1.Vector v) => new Point3D(v.x, v.y, v.z);
        public static Vector3D ToVector3D(this Md1.Vector v) => new Vector3D(v.x, v.y, v.z);
    }
}
