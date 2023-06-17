using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using emdui.Extensions;
using IntelOrca.Biohazard;
using Microsoft.Win32;

namespace emdui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Project _project;

        private ModelFile _modelFile;

        private Md1 _md1;
        private Md2 _md2;
        private Edd _edd;
        private Emr _emr;
        private TimFile _tim;

        private ModelScene _scene;
        private DispatcherTimer _timer;

        private int _animationIndex;
        private double _time;

        public MainWindow()
        {
            InitializeComponent();
            Work();
            // viewport0.SetCameraOrthographic(new Vector3D(-1, 0, 0));
            viewport1.SetCameraOrthographic(new Vector3D(0, 0, 1));
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Interval = TimeSpan.FromMilliseconds(40);
            _timer.Tick += _timer_Tick;
            _timer.IsEnabled = true;

            projectTreeView.MainWindow = this;
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            if (_modelFile == null || _animationIndex == -1)
                return;

            var edd = _edd;

            if (animationDropdown.Items.Count != edd.AnimationCount)
            {
                animationDropdown.Items.Clear();
                for (var i = 0; i < edd.AnimationCount; i++)
                {
                    animationDropdown.Items.Add($"Animation {i}");
                }
            }

            if (_animationIndex != animationDropdown.SelectedIndex)
            {
                animationDropdown.SelectedIndex = _animationIndex;
                // if (animationDropdown.SelectedIndex == -1)
                // {
                //     animationDropdown.SelectedIndex = 0;
                // }
                // _animationIndex = animationDropdown.SelectedIndex;
            }

            var frames = edd.GetFrames(_animationIndex);
            while (_time >= frames.Length)
            {
                _time -= frames.Length;
            }

            var animationKeyframe = (int)_time;
            var emrKeyframeIndex = frames[animationKeyframe].Index;

            timeTextBlock.Text = _time.ToString("0.00");
            _scene.SetKeyframe(emrKeyframeIndex);

            _time++;
        }

        private void Work()
        {
        }

        private void SetTimFile(TimFile timFile)
        {
            _tim = timFile;
            if (_modelFile is PldFile pldFile)
                pldFile.SetTim(timFile);
            RefreshTimImage();
        }

        private void LoadProject(string path)
        {
            _project = Project.FromFile(path);
            projectTreeView.Project = _project;

            LoadModel(_project.MainModel, _project.MainTexture);

            RefreshParts();
            RefreshStatusBar();
        }

        private void SaveModel(string path)
        {
            _project.Save(path);
        }

        private void ImportModel(string path)
        {
            if (path.EndsWith(".obj", StringComparison.CurrentCultureIgnoreCase))
            {
                var numPages = _tim.Width / 128;
                var objImporter = new ObjImporter();
                _modelFile.Md1 = objImporter.ImportMd1(path, numPages, GetFinalPosition);
            }
            else
            {
                var modelFile = ModelFile.FromFile(path);
                if (modelFile.Version == BioVersion.Biohazard2)
                {
                    if (_modelFile.Version == BioVersion.Biohazard2)
                    {
                        _modelFile.Md1 = modelFile.Md1;
                        _modelFile.SetEmr(0, modelFile.GetEmr(0));
                    }
                    else
                    {
                        _modelFile.Md2 = modelFile.Md1.ToMd2();

                        var map2to3 = new[]
                        {
                            0, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7
                        };
                        var emr = modelFile.GetEmr(0);
                        var emrBuilder = _modelFile.GetEmr(0).ToBuilder();
                        for (var i = 0; i < map2to3.Length; i++)
                        {
                            var srcPartIndex = i;
                            var dstPartIndex = map2to3[i];
                            var src = emr.GetRelativePosition(srcPartIndex);
                            emrBuilder.RelativePositions[dstPartIndex] = src;
                        }
                        _modelFile.SetEmr(0, emrBuilder.ToEmr());
                    }
                }
                else
                {
                    if (_modelFile.Version == BioVersion.Biohazard2)
                    {
                        _modelFile.Md1 = modelFile.Md2.ToMd1();
                    }
                    else
                    {
                        _modelFile.Md2 = modelFile.Md2;

                        var emr = modelFile.GetEmr(0);
                        var emrBuilder = _modelFile.GetEmr(0).ToBuilder();
                        for (var i = 0; i < 15; i++)
                        {
                            emrBuilder.RelativePositions[i] = emr.GetRelativePosition(i);
                        }
                        _modelFile.SetEmr(0, emrBuilder.ToEmr());
                    }
                }

                if (modelFile is PldFile pldFile)
                {
                    SetTimFile(pldFile.GetTim());
                }
                else
                {
                    var timPath = Path.ChangeExtension(path, ".tim");
                    if (File.Exists(timPath))
                    {
                        SetTimFile(new TimFile(timPath));
                    }
                }
            }
            RefreshModelView();
        }

        private void ExportModel(string path)
        {
            var numPages = _tim.Width / 128;
            var objExporter = new ObjExporter();
            objExporter.Export(_modelFile.Md1, path, numPages, GetFinalPosition);

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

        private void SaveTim(string path)
        {
            _tim.Save(path);
        }

        private void ExportTim(string path)
        {
            if (path.EndsWith(".tim", StringComparison.OrdinalIgnoreCase))
            {
                _tim.Save(path);
            }
            else
            {
                _tim.ToBitmap().Save(path);
            }
        }

        private void RefreshTimImage()
        {
            timImage.Tim = null;
            timImage.Tim = _tim;
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

        private string[] g_partNamesRe3 = new string[]
        {
            "chest", "head",
            "upper arm (right)", "forearm (right)", "hand (right)",
            "upper arm (left)", "forearm (left)", "hand (left)",
            "waist",
            "thigh (right)", "calf (right)", "foot (right)",
            "thigh (left)", "calf (left)", "foot (left)",
            "hand with gun"
        };

        private string GetPartName(int partIndex)
        {
            if (_modelFile.Version == BioVersion.Biohazard2)
            {
                if (g_partNamesRe2.Length > partIndex)
                    return g_partNamesRe2[partIndex];
            }
            else
            {
                if (g_partNamesRe3.Length > partIndex)
                    return g_partNamesRe3[partIndex];
            }
            return $"part {partIndex}";
        }

        private int GetNumParts()
        {
            if (_modelFile.Version == BioVersion.Biohazard2)
                return _modelFile.Md1.NumObjects / 2;
            return _modelFile.Md2.NumObjects;
        }

        private void RefreshParts()
        {
            var emr = _emr;
            var numParts = GetNumParts();
            var rootPartIndex = 0;

            var items = new TreeViewItem[emr.NumParts];
            var parent = new TreeViewItem[emr.NumParts];
            var stack = new Stack<byte>();
            if (emr.NumParts != 0)
            {
                stack.Push((byte)rootPartIndex);
            }
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

            if (emr.NumParts != 0)
            {
                root.Items.Add(items[rootPartIndex]);
            }
            for (int i = 0; i < numParts; i++)
            {
                if (i < items.Length && items[i] != null)
                    continue;

                root.Items.Add(new TreeViewItem()
                {
                    Header = GetPartName(i),
                    Tag = (int)i,
                    IsExpanded = true
                });
            }

            treeParts.Items.Clear();
            treeParts.Items.Add(root);

            RefreshModelView();
        }

        private void RefreshModelView()
        {
            _scene = new ModelScene();
            if (_project.Version == BioVersion.Biohazard2)
                _scene.GenerateFrom(_md1, _emr, _tim);
            else
                _scene.GenerateFrom(_md2, _emr, _tim);
            viewport0.Scene = _scene;
            viewport1.Scene = _scene;

            RefreshHighlightedPart();
            RefreshRelativePositionTextBoxes();
        }

        private void RefreshHighlightedPart()
        {
            var partIndex = GetSelectedPartIndex();
            _scene.HighlightPart(partIndex);

            if (partIndex == -1)
            {
                timImage.Primitives = null;
                return;
            }

            if (_modelFile.Version == BioVersion.Biohazard2)
            {
                var md1 = _modelFile.Md1;
                var objTriangle = md1.Objects[partIndex * 2];
                var objQuad = md1.Objects[(partIndex * 2) + 1];
                var triangleTextures = md1.GetTriangleTextures(in objTriangle);
                var quadTextures = md1.GetQuadTextures(in objQuad);
                var primitives = new List<TimView.UVPrimitive>();
                foreach (var t in triangleTextures)
                {
                    var uv = new TimView.UVPrimitive();
                    uv.Page = (byte)(t.page & 0xF);
                    uv.U0 = t.u0;
                    uv.V0 = t.v0;
                    uv.U1 = t.u1;
                    uv.V1 = t.v1;
                    uv.U2 = t.u2;
                    uv.V2 = t.v2;
                    primitives.Add(uv);
                }
                foreach (var t in quadTextures)
                {
                    var uv = new TimView.UVPrimitive();
                    uv.IsQuad = true;
                    uv.Page = (byte)(t.page & 0xF);
                    uv.U0 = t.u0;
                    uv.V0 = t.v0;
                    uv.U1 = t.u1;
                    uv.V1 = t.v1;
                    uv.U2 = t.u2;
                    uv.V2 = t.v2;
                    uv.U3 = t.u3;
                    uv.V3 = t.v3;
                    primitives.Add(uv);
                }
                timImage.Primitives = primitives.ToArray();
            }
            else
            {

            }

        }

        private void RefreshRelativePositionTextBoxes()
        {
            var emr = _emr;
            var partIndex = GetSelectedPartIndex();
            if (partIndex != -1 && partIndex < emr.NumParts)
            {
                var pos = emr.GetRelativePosition(partIndex);
                partXTextBox.Text = pos.x.ToString();
                partYTextBox.Text = pos.y.ToString();
                partZTextBox.Text = pos.z.ToString();
            }
        }

        private void RefreshStatusBar()
        {
            var game = _modelFile.Version == BioVersion.Biohazard2 ? "RE 2" : "RE 3";
            var fileType = _modelFile is EmdFile ? ".EMD" : ".PLD";
            fileTypeLabel.Content = $"{game} {fileType} File";
            numPartsLabel.Content = $"{GetNumParts()} parts";
        }

        private void RefreshPrimitives()
        {
            var md1 = _modelFile.Md1;
            var selectedIndex = GetSelectedPartIndex() * 2;
            if (selectedIndex >= 0 && selectedIndex < md1.NumObjects)
            {
                var objTri = md1.Objects[selectedIndex + 0];
                var objQuad = md1.Objects[selectedIndex + 1];
                var triangles = md1.GetTriangles(objTri);
                var quads = md1.GetQuads(objQuad);

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
#if DEBUG
            // LoadProject(@"M:\git\rer\IntelOrca.Biohazard\data\re2\pld0\barry\pl00.pld");
            // LoadProject(@"M:\git\rer\IntelOrca.Biohazard\data\re3\pld0\hunk\PL00.PLD");
            // LoadProject(@"M:\git\rer\IntelOrca.Biohazard\data\re3\pld0\leon\PL00.PLD");
            // LoadProject(@"M:\git\rer\IntelOrca.Biohazard\data\re2\pld0\chris\PL00.PLD");
            LoadProject(@"F:\games\re3\mod_biorand\DATA\PLD\PL00.PLD");
#endif
        }

        private void menuOpen_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_project.MainPath);
            openFileDialog.Filter = "EMD/PLD Files (*.emd;*.pld)|*.emd;*.pld";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadProject(openFileDialog.FileName);
            }
        }

        private void menuSave_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_project.MainPath);
            if (_modelFile is PldFile)
            {
                saveFileDialog.Filter = "PLD Files (*.pld)|*.pld";
            }
            else
            {
                saveFileDialog.Filter = "EMD Files (*.emd)|*.emd";
            }
            saveFileDialog.FileName = _project.MainPath;
            if (saveFileDialog.ShowDialog() == true)
            {
                SaveModel(saveFileDialog.FileName);
            }
        }

        private void treeParts_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RefreshHighlightedPart();
            RefreshRelativePositionTextBoxes();
        }

        private void listPrimitives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var md1 = _modelFile.Md1;
            var objIndex = GetSelectedPartIndex() * 2;
            if (objIndex >= 0 && objIndex < md1.NumObjects)
            {
                var objTri = md1.Objects[objIndex + 0];
                var objQuad = md1.Objects[objIndex + 1];
                var triangles = md1.GetTriangles(objTri);
                var triangleTextures = md1.GetTriangleTextures(objTri);
                var quads = md1.GetQuads(objQuad);
                var quadTextures = md1.GetQuadTextures(objQuad);

                var priIndex = listPrimitives.SelectedIndex;
                if (priIndex >= 0 && priIndex < triangles.Length)
                {
                    var tri = triangles[priIndex];
                    var triTex = triangleTextures[priIndex];
                    var positionData = md1.GetPositionData(objTri);
                    var v0 = positionData[tri.v0];
                    var v1 = positionData[tri.v1];
                    var v2 = positionData[tri.v2];
                    // textPrimitive.Text = string.Join("\n", new[] {
                    //     $"v0 = ({v0.x}, {v0.y}, {v0.z})",
                    //     $"v1 = ({v1.x}, {v1.y}, {v1.z})",
                    //     $"v2 = ({v2.x}, {v2.y}, {v2.z})"
                    // });
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
                    // textPrimitive.Text = string.Join("\n", new[] {
                    //     quad.n0,
                    //     quad.v0,
                    //     quad.n1,
                    //     quad.v1,
                    //     quad.n2,
                    //     quad.v2,
                    //     quad.n3,
                    //     quad.v3
                    // });
                }
            }
        }

        private void menuAddDummyPart_Click(object sender, RoutedEventArgs e)
        {
            var md1 = _modelFile.Md1;
            var md1Builder = md1.ToBuilder();

            var part = new Md1Builder.Part();
            part.Positions.Add(new Md1.Vector());
            part.Normals.Add(new Md1.Vector());
            part.Triangles.Add(new Md1.Triangle());
            part.TriangleTextures.Add(new Md1.TriangleTexture());
            md1Builder.Parts.Add(part);
            _modelFile.Md1 = md1Builder.ToMd1();
            RefreshParts();
            RefreshStatusBar();
        }

        private void menuCopyPage_Click(object sender, RoutedEventArgs e)
        {
            if (_tim.Width < 3 * 128)
            {
                _tim.ResizeImage(3 * 128, _tim.Height);
            }
            _tim.SetPalette(2, _tim.GetPalette(0));

            for (int x = 0; x < 128; x++)
            {
                for (int y = 0; y < _tim.Height; y++)
                {
                    var p = _tim.GetPixel(x, y, 0);
                    _tim.SetPixel(256 + x, y, 2, p);
                }
            }
            SetTimFile(_tim);
        }

        private void menuExportModel_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_project.MainPath);
            saveFileDialog.Filter = "Wavefront .obj Files (*.obj)|*.obj";
            if (saveFileDialog.ShowDialog() == true)
            {
                ExportModel(saveFileDialog.FileName);
            }
        }

        private void menuImportModel_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_project.MainPath);
            openFileDialog.Filter = "All Supported Files (*.emd;*.pld;*.obj)|*.emd;*.pld;*.obj";
            openFileDialog.Filter += "|EMD/PLD Files (*.emd;*.pld)|*.emd;*.pld";
            openFileDialog.Filter += "|Wavefront .obj Files (*.obj)|*.obj";
            if (openFileDialog.ShowDialog() == true)
            {
                ImportModel(openFileDialog.FileName);
            }
        }

        private void menuAutoHandWithGun_Click(object sender, RoutedEventArgs e)
        {
            if (_modelFile.Version != BioVersion.Biohazard3 ||
                _modelFile is PldFile)
            {
                MessageBox.Show("This feature is for RE 3 EMDs only", "Not applicable", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var builder = _modelFile.Md2.ToBuilder();
            if (builder.Parts.Count < 16)
            {
                var part = new Md2Builder.Part();
                part.Positions.Add(new Md2.Vector());
                part.Normals.Add(new Md2.Vector());
                part.Triangles.Add(new Md2.Triangle());
                builder.Parts.Add(part);
            }
            builder.Parts[15] = builder.Parts[4];
            _modelFile.Md2 = builder.ToMd2();
            RefreshModelView();
            RefreshStatusBar();
        }

        private void timImage_TimUpdated(object sender, EventArgs e)
        {
            SetTimFile(timImage.Tim);
            RefreshModelView();
        }

        public void LoadIsolatedModel(Md1 md1, TimFile texture)
        {
            _modelFile = null;
            _md1 = md1;
            _tim = texture;
            _edd = null;
            _emr = null;
            RefreshModelView();
            RefreshTimImage();
        }

        public void LoadIsolatedModel(Md2 md2, TimFile texture)
        {
            _modelFile = null;
            _md2 = md2;
            _tim = texture;
            _edd = null;
            _emr = null;
            RefreshModelView();
            RefreshTimImage();
        }

        public void LoadModel(ModelFile model, TimFile texture)
        {
            _modelFile = model;
            if (_project.Version == BioVersion.Biohazard2)
                _md1 = model.Md1;
            else
                _md2 = model.Md2;
            _tim = texture;
            _edd = _modelFile.GetEdd(0);
            _emr = _modelFile.GetEmr(0);
            _animationIndex = -1;
            RefreshModelView();
            RefreshTimImage();
        }

        public void LoadWeaponModel(PlwFile plw)
        {
            _edd = plw.GetEdd(0);
            _emr = _emr.WithKeyframes(plw.GetEmr(0));
            _animationIndex = -1;

            var targetBuilder = _md1.ToBuilder();
            var sourceBuilder = plw.Md1.ToBuilder();
            targetBuilder.Parts[11] = sourceBuilder.Parts[0];
            _md1 = targetBuilder.ToMd1();

            var tim = _tim;
            var plwTim = plw.Tim;
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 56; x++)
                {
                    var p = plwTim.GetPixel(x, y);
                    tim.SetPixel(200 + x, 224 + y, 1, p);
                }
            }
            RefreshModelView();
            RefreshTimImage();
        }

        public void LoadAnimation(int index)
        {
            _animationIndex = index;
            _time = 0;
        }
    }
}
