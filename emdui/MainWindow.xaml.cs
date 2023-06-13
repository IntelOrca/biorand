using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
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

        private Emr _emr;
        private Md1 _md1;
        private BitmapSource _timImage;

        private Point3D _cameraLookAt = new Point3D(0, -1000, 0);
        private double _cameraZoom = 10000;
        private double _cameraAngleH;
        private double _cameraAngleV;

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
                var emdFile = new EmdFile(BioVersion.Biohazard2, _path);
                _modelFile = emdFile;
                _emr = emdFile.GetEmr(0);
                var timPath = Path.ChangeExtension(path, ".tim");
                if (File.Exists(timPath))
                {
                    LoadTim(timPath);
                }
            }
            else
            {
                var pldFile = new PldFile(BioVersion.Biohazard2, _path);
                _modelFile = pldFile;
                _emr = pldFile.GetEmr();
                _timFile = pldFile.GetTim();
                RefreshTimImage();
            }

            _md1 = _modelFile.Md1;
            RefreshParts();
        }

        private void SaveModel(string path)
        {
            _path = path;

            if (path.EndsWith(".emd", StringComparison.OrdinalIgnoreCase))
            {
                _modelFile.Save(_path);
                var timPath = Path.ChangeExtension(path, ".tim");
                SaveTim(timPath);
            }
            else
            {
                _modelFile.Save(_path);
            }
        }

        private void ImportModel(string path)
        {
            var numPages = _timFile.Width / 128;
            var objImporter = new ObjImporter();
            _md1 = objImporter.ImportMd1(path, numPages, GetFinalPosition);
            _modelFile.Md1 = _md1;

            RefreshModelView();
        }

        private void ExportModel(string path)
        {
            var numPages = _timFile.Width / 128;
            var objExporter = new ObjExporter();
            objExporter.Export(_md1, path, numPages, GetFinalPosition);

            var timPath = Path.ChangeExtension(path, ".png");
            ExportTim(timPath);
        }

        private Md1.Vector GetFinalPosition(int targetPartIndex)
        {
            var emr = _emr;
            if (targetPartIndex < 0 || targetPartIndex >= emr.NumParts)
                return new Md1.Vector();

            var positions = new Md1.Vector[emr.NumParts];

            var stack = new Stack<byte>();
            stack.Push(0);

            while (stack.Count != 0)
            {
                var partIndex = stack.Pop();
                var pos = positions[partIndex];
                var rel = emr.GetRelativePosition(partIndex);
                pos.x += rel.x;
                pos.y += rel.y;
                pos.z += rel.z;
                positions[partIndex] = pos;
                var children = emr.GetArmatureParts(partIndex);
                foreach (var child in children)
                {
                    positions[child] = pos;
                    stack.Push(child);
                }
            }

            return positions[targetPartIndex];
        }

        private void LoadTim(string path)
        {
            _timFile = new TimFile(path);
            RefreshTimImage();
        }

        private void SaveTim(string path)
        {
            _timFile.Save(path);
        }

        private void ExportTim(string path)
        {
            var bitmapEncoder = new PngBitmapEncoder();
            bitmapEncoder.Frames.Add(BitmapFrame.Create(_timImage));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                bitmapEncoder.Save(fs);
            }
        }

        private void RefreshTimImage()
        {
            var pixels = _timFile.GetPixels((x, y) => x / 128);
            timImage.Width = _timFile.Width;
            timImage.Height = _timFile.Height;
            timImage.Source = _timImage = BitmapSource.Create(_timFile.Width, _timFile.Height, 96, 96, PixelFormats.Bgra32, null, pixels, _timFile.Width * 4);
        }

        private string[] g_partNamesRe2 = new string[]
        {
            "chest", "waist",
            "thigh (right)", "calf (right)", "foot (right)",
            "thigh (left)", "calf (left)", "foot (left)",
            "head",
            "upper arm (right)", "forearm (right)", "hand (right)",
            "upper arm (left)", "forearm (left)", "hand (left)",
            "ponytail (A)", "ponytail (B)", "ponytail (C)", "ponytail (D)"
        };

        private string GetPartName(int partIndex)
        {
            if (g_partNamesRe2.Length > partIndex)
                return g_partNamesRe2[partIndex];
            return $"part {partIndex}";
        }

        private void RefreshParts()
        {
            var emr = _emr;
            var rootPartIndex = 0;

            var items = new TreeViewItem[emr.NumParts];
            var parent = new TreeViewItem[emr.NumParts];
            var stack = new Stack<byte>();
            stack.Push((byte)rootPartIndex);
            while (stack.Count != 0)
            {
                var partIndex = stack.Pop();
                items[partIndex] = new TreeViewItem()
                {
                    Header = GetPartName(partIndex),
                    Tag = (int)partIndex,
                    IsExpanded = true
                };
                if (parent[partIndex] is TreeViewItem parentItem)
                {
                    parentItem.Items.Add(items[partIndex]);
                }

                var children = emr.GetArmatureParts(partIndex);
                foreach (var child in children)
                {
                    parent[child] = items[partIndex];
                    stack.Push(child);
                }
            }

            var root = new TreeViewItem();
            root.Header = "Skeleton";
            root.IsExpanded = true;

            root.Items.Add(items[rootPartIndex]);
            for (int i = 0; i < _md1.NumObjects; i += 2)
            {
                var partIndex = i / 2;
                if (partIndex >= items.Length || items[partIndex] == null)
                {
                    root.Items.Add(new TreeViewItem()
                    {
                        Header = GetPartName(partIndex),
                        Tag = (int)partIndex,
                        IsExpanded = true
                    });
                }
            }

            treeParts.Items.Clear();
            treeParts.Items.Add(root);

            RefreshModelView();
        }

        private void RefreshModelView()
        {
            var partIndex = GetSelectedPartIndex();
            if (partIndex != -1 && partIndex < _emr.NumParts)
            {
                var pos = _emr.GetRelativePosition(partIndex);
                partXTextBox.Text = pos.x.ToString();
                partYTextBox.Text = pos.y.ToString();
                partZTextBox.Text = pos.z.ToString();
            }

            // var camera = myViewport.Camera;
            var children = myViewport.Children;
            children.Clear();

            var modelVisual = new ModelVisual3D();
            modelVisual.Content = CreateModel();

            children.Add(modelVisual);

            var camera = new PerspectiveCamera(
                new Point3D(-5000, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, -1, 0),
                70);

            // var camera = new OrthographicCamera(
            //     new Point3D(-1000, 0, 0),
            //     new Vector3D(1, 0, 0),
            //     new Vector3D(0, -1, 0),
            //     5000);
            // camera.FarPlaneDistance = 5000;
            // camera.NearPlaneDistance = 1;

            myViewport.Camera = camera;

            myViewport.Children.Add(
                new ModelVisual3D() { Content = new AmbientLight(Colors.White) });

            UpdateCamera();
        }

        private Model3DGroup CreateModel()
        {
            var rootGroup = new Model3DGroup();
            var main = CreateModelFromArmature(0);
            rootGroup.Children.Add(main);
            var mainParts = GetAllArmatureParts(0);
            for (var i = 0; i < _md1.NumObjects; i += 2)
            {
                var partIndex = i / 2;
                if (!mainParts.Contains(partIndex))
                {
                    var model = CreateModelFromPart(partIndex);
                    rootGroup.Children.Add(model);
                }
            }
            return rootGroup;
        }

        private int[] GetAllArmatureParts(int rootPartIndex)
        {
            var emr = _emr;
            var parts = new List<int>();
            var stack = new Stack<byte>();
            stack.Push((byte)rootPartIndex);
            while (stack.Count != 0)
            {
                var partIndex = stack.Pop();
                parts.Add(partIndex);

                var children = emr.GetArmatureParts(partIndex);
                foreach (var child in children)
                {
                    stack.Push(child);
                }
            }
            return parts.ToArray();
        }

        private Model3DGroup CreateModelFromArmature(int partIndex)
        {
            var armature = new Model3DGroup();
            var armatureMesh = CreateModelFromPart(partIndex);
            armature.Children.Add(armatureMesh);

            // Children
            var subParts = _emr.GetArmatureParts(partIndex);
            foreach (var subPart in subParts)
            {
                var subPartMesh = CreateModelFromArmature(subPart);
                armature.Children.Add(subPartMesh);
            }

            var relativePosition = _emr.GetRelativePosition(partIndex);
            armature.Transform = new TranslateTransform3D(relativePosition.x, relativePosition.y, relativePosition.z);
            return armature;
        }

        private GeometryModel3D CreateModelFromPart(int partIndex)
        {
            var textureWidth = (double)_timImage.PixelWidth;
            var textureHeight = (double)_timImage.PixelHeight;
            var mesh = new MeshGeometry3D();

            // Triangles
            {
                var objTriangles = _md1.Objects[partIndex * 2];
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

            // Quads
            {
                var objQuads = _md1.Objects[(partIndex * 2) + 1];
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

                    mesh.Normals.Add(dataNormals[quad.n0].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n1].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n2].ToVector3D());

                    var page = texture.page & 0x0F;
                    var offsetU = page * 128;
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u0) / textureWidth, (texture.v0 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u1) / textureWidth, (texture.v1 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u2) / textureWidth, (texture.v2 / textureHeight)));

                    mesh.Positions.Add(dataPositions[quad.v3].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v2].ToPoint3D());
                    mesh.Positions.Add(dataPositions[quad.v1].ToPoint3D());
                    mesh.Normals.Add(dataNormals[quad.n3].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n2].ToVector3D());
                    mesh.Normals.Add(dataNormals[quad.n1].ToVector3D());
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u3) / textureWidth, (texture.v3 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u2) / textureWidth, (texture.v2 / textureHeight)));
                    mesh.TextureCoordinates.Add(new Point((offsetU + texture.u1) / textureWidth, (texture.v1 / textureHeight)));
                }
            }

            var material = new DiffuseMaterial(System.Windows.Media.Brushes.Red);
            material.Brush = new ImageBrush(_timImage)
            {
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute
            };
            if (GetSelectedPartIndex() == partIndex)
            {
                material.AmbientColor = Colors.Blue;
            }

            var model = new GeometryModel3D();
            model.Geometry = mesh;
            model.BackMaterial = material;
            model.Material = material;
            // model.Material = new DiffuseMaterial(Brushes.Yellow);
            return model;
        }

        private void RefreshPrimitives()
        {
            var selectedIndex = GetSelectedPartIndex() * 2;
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

        private int GetSelectedPartIndex()
        {
            if (treeParts.SelectedItem is TreeViewItem item)
            {
                if (item.Tag is int partIndex)
                {
                    return partIndex;
                }
            }
            return -1;
        }

        private void mainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // LoadModel(@"M:\git\rer\IntelOrca.Biohazard\data\re2\pld0\barry\pl00.pld");
            LoadModel(@"F:\games\re2\data\Pl0\emd0\em03e.emd");
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
            if (_modelFile is PldFile)
            {
                saveFileDialog.Filter = "PLD Files (*.pld)|*.pld";
            }
            else
            {
                saveFileDialog.Filter = "EMD Files (*.emd)|*.emd";
            }
            saveFileDialog.FileName = _path;
            if (saveFileDialog.ShowDialog() == true)
            {
                SaveModel(saveFileDialog.FileName);
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
                UpdateTimFile();
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
                ExportTim(saveFileDialog.FileName);
            }
        }

        private void treeParts_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RefreshModelView();
        }

        private void listPrimitives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var objIndex = GetSelectedPartIndex() * 2;
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

        private void menuAddDummyPart_Click(object sender, RoutedEventArgs e)
        {
            var md1Builder = _md1.ToBuilder();

            var part = new Md1Builder.Part();
            part.Positions.Add(new Md1.Vector());
            part.Normals.Add(new Md1.Vector());
            part.Triangles.Add(new Md1.Triangle());
            part.TriangleTextures.Add(new Md1.TriangleTexture());
            md1Builder.Parts.Add(part);
            _md1 = md1Builder.ToMd1();
            _modelFile.Md1 = _md1;
            RefreshParts();
        }

        private void menuCopyPage_Click(object sender, RoutedEventArgs e)
        {
            if (_timFile.Width < 3 * 128)
            {
                _timFile.ResizeImage(3 * 128, _timFile.Height);
            }
            _timFile.SetPalette(2, _timFile.GetPalette(0));

            for (int x = 0; x < 128; x++)
            {
                for (int y = 0; y < _timFile.Height; y++)
                {
                    var p = _timFile.GetPixel(x, y, 0);
                    _timFile.SetPixel(256 + x, y, 2, p);
                }
            }
            UpdateTimFile();
        }

        private void UpdateTimFile()
        {
            if (_modelFile is PldFile pld)
            {
                pld.SetTim(_timFile);
            }
            RefreshTimImage();
        }

        private Point _cameraPointerStartPosition;
        private Point3D _cameraStartPosition;
        private double _cameraStartWidth;
        private double _startCameraAngleH;
        private double _startCameraAngleV;

        private void myViewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (myViewport.Camera is PerspectiveCamera pcamera)
            {
                _cameraPointerStartPosition = e.GetPosition(myViewport);
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    _startCameraAngleH = _cameraAngleH;
                    _startCameraAngleV = _cameraAngleV;
                }
                if (e.MiddleButton == MouseButtonState.Pressed)
                {
                    _cameraStartPosition = _cameraLookAt;
                }
            }
            else if (myViewport.Camera is OrthographicCamera camera)
            {
                _cameraPointerStartPosition = e.GetPosition(myViewport);
                _cameraStartPosition = camera.Position;
                _cameraStartWidth = camera.Width;
            }
        }

        private void myViewport_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(myViewport);
            var diff = position - _cameraPointerStartPosition;

            if (myViewport.Camera is OrthographicCamera camera)
            {
                if (e.MouseDevice.MiddleButton == MouseButtonState.Pressed)
                {
                    var newCameraPosition = _cameraStartPosition;
                    newCameraPosition.Z += diff.X * 6;
                    newCameraPosition.Y -= diff.Y * 6;
                    camera.Position = newCameraPosition;
                }
            }
            else if (myViewport.Camera is PerspectiveCamera pcamera)
            {
                if (e.MouseDevice.LeftButton == MouseButtonState.Pressed)
                {
                    _cameraAngleH = _startCameraAngleH + (diff.X / 100);
                    _cameraAngleV = _startCameraAngleV + (diff.Y / 100);
                    UpdateCamera();
                }
                if (e.MouseDevice.MiddleButton == MouseButtonState.Pressed)
                {
                    var newCameraPosition = _cameraStartPosition;
                    newCameraPosition.Z += diff.X * 6;
                    newCameraPosition.Y -= diff.Y * 6;
                    _cameraLookAt = newCameraPosition;
                    UpdateCamera();
                }
            }
        }

        public static void Rotate(PerspectiveCamera camera, Vector3D axis, double angle)
        {
            var matrix3D = new Matrix3D();
            matrix3D.RotateAt(new Quaternion(axis, angle), camera.Position);
            camera.LookDirection *= matrix3D;
        }

        private void myViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (myViewport.Camera is OrthographicCamera camera)
            {
                camera.Width = Math.Max(2000, camera.Width - e.Delta * 4);
            }
            else if (myViewport.Camera is PerspectiveCamera pcamera)
            {
                _cameraZoom = Math.Max(1000, _cameraZoom - (e.Delta * 10));
                UpdateCamera();
            }
        }

        private void UpdateCamera()
        {
            if (myViewport.Camera is PerspectiveCamera camera)
            {
                // var rx = _cameraZoom * (Math.Cos(_cameraAngleH) - Math.Sin(_cameraAngleV));
                // var ry = _cameraZoom * Math.Sin(_cameraAngleV);
                // var rz = _cameraZoom * Math.Sin(_cameraAngleH);

                var hoizontalVector = new Vector3D(
                    Math.Cos(_cameraAngleH),
                    0,
                    Math.Sin(_cameraAngleH));
                var verticalVector = new Vector3D(
                    Math.Cos(_cameraAngleV),
                    -Math.Sin(_cameraAngleV),
                    0);

                var merged = hoizontalVector + verticalVector;
                merged.Normalize();
                var position = merged * _cameraZoom;
                camera.Position = new Point3D(position.X, position.Y, position.Z);

                var look = _cameraLookAt - camera.Position;
                look.Normalize();
                camera.LookDirection = look;
            }
        }

        private void menuExportModel_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            saveFileDialog.Filter = "Wavefront .obj Files (*.obj)|*.obj";
            if (saveFileDialog.ShowDialog() == true)
            {
                ExportModel(saveFileDialog.FileName);
            }
        }

        private void menuImportModel_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_path);
            openFileDialog.Filter = "Wavefront .obj Files (*.obj)|*.obj";
            if (openFileDialog.ShowDialog() == true)
            {
                ImportModel(openFileDialog.FileName);
            }
        }
    }

    internal static class Md1Extensions
    {
        public static Point3D ToPoint3D(this Md1.Vector v) => new Point3D(v.x, v.y, v.z);
        public static Vector3D ToVector3D(this Md1.Vector v) => new Vector3D(v.x, v.y, v.z);
    }
}
