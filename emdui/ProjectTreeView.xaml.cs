using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using emdui.Extensions;
using IntelOrca.Biohazard;

namespace emdui
{
    /// <summary>
    /// Interaction logic for ProjectTreeView.xaml
    /// </summary>
    public partial class ProjectTreeView : UserControl
    {
        private Project _project;

        public MainWindow MainWindow { get; set; }

        public ProjectTreeView()
        {
            InitializeComponent();
        }

        private void Refresh()
        {
            treeView.ItemsSource = null;
            if (_project == null)
                return;

            var items = new List<ProjectTreeViewItem>();
            foreach (var projectFile in _project.Files)
            {
                if (projectFile.Content is PldFile pld)
                {
                    items.Add(new PldTreeViewItem(projectFile));
                }
                else if (projectFile.Content is PlwFile plw)
                {
                    items.Add(new PlwTreeViewItem(projectFile));
                }
                else if (projectFile.Content is EmdFile emd)
                {
                    items.Add(new EmdTreeViewItem(projectFile));
                }
                else if (projectFile.Content is TimFile tim)
                {
                    items.Add(new TimTreeViewItem(projectFile, tim));
                }
            }
            treeView.ItemsSource = items;
        }

        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }

        private void treeView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeView.SelectedItem is ProjectTreeViewItem tvi)
            {
                tvi.Select();
                treeView.ContextMenu = tvi.GetContextMenu();
            }
        }

        private void treeView_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (treeView.SelectedItem is ProjectTreeViewItem tvi)
            {
                tvi.ExecuteDefaultAction();
                e.Handled = true;
            }
        }

        public Project Project
        {
            get => _project;
            set
            {
                if (_project != value)
                {
                    _project = value;
                    Refresh();
                }
            }
        }
    }

    public abstract class ProjectTreeViewItem : INotifyPropertyChanged
    {
        private readonly List<Tuple<string, Action>> _menuItems = new List<Tuple<string, Action>>();

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual ImageSource Image => (ImageSource)App.Current.Resources["IconPLD"];
        public abstract string Header { get; }
        public virtual ObservableCollection<ProjectTreeViewItem> Items { get; } = new ObservableCollection<ProjectTreeViewItem>();
        public ProjectFile ProjectFile { get; }
        public ModelFile Model => ProjectFile.Content as ModelFile;

        public ProjectTreeViewItem(ProjectFile projectFile)
        {
            ProjectFile = projectFile;
        }

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void AddMenuItem(string header, Action callback)
        {
            _menuItems.Add(new Tuple<string, Action>(header, callback));
        }

        protected void AddSeperator()
        {
            _menuItems.Add(null);
        }

        public void ExecuteDefaultAction()
        {
            OnDefaultAction();
        }

        public void Select()
        {
            OnSelect();
        }

        public virtual void OnDefaultAction()
        {
        }

        public virtual void OnSelect()
        {
        }

        public ContextMenu GetContextMenu()
        {
            if (_menuItems.Count == 0)
                return null;

            var contextMenu = new ContextMenu();
            foreach (var menuItem in _menuItems)
            {
                if (menuItem == null)
                {
                    contextMenu.Items.Add(new Separator());
                }
                else
                {
                    var item = new MenuItem()
                    {
                        Header = menuItem.Item1
                    };
                    item.Click += (s, e) => menuItem.Item2();
                    contextMenu.Items.Add(item);
                }
            }
            return contextMenu;
        }
    }

    public class PldTreeViewItem : ProjectTreeViewItem
    {
        public override string Header => ProjectFile.Filename;
        public PldFile Pld { get; }

        public PldTreeViewItem(ProjectFile projectFile)
            : base(projectFile)
        {
            Pld = (PldFile)projectFile.Content;
            Items.Add(new EddTreeViewItem(ProjectFile, Pld.GetEdd(0), 0));
            Items.Add(new EmrTreeViewItem(ProjectFile, Pld.GetEmr(0), 0));
            if (Pld.Version == BioVersion.Biohazard2)
                Items.Add(new MeshTreeViewItem(ProjectFile, Pld.Md1));
            else
                Items.Add(new MeshTreeViewItem(ProjectFile, Pld.Md2));
            Items.Add(new TimTreeViewItem(ProjectFile, Pld.Tim));
        }
    }

    public class EmdTreeViewItem : ProjectTreeViewItem
    {
        public override string Header => ProjectFile.Filename;
        public EmdFile Emd { get; }

        public EmdTreeViewItem(ProjectFile projectFile)
            : base(projectFile)
        {
            Emd = (EmdFile)projectFile.Content;
            Items.Add(new EddTreeViewItem(ProjectFile, Emd.GetEdd(0), 0));
            Items.Add(new EmrTreeViewItem(ProjectFile, Emd.GetEmr(0), 0));
            Items.Add(new EddTreeViewItem(ProjectFile, Emd.GetEdd(1), 1));
            Items.Add(new EmrTreeViewItem(ProjectFile, Emd.GetEmr(1), 1));
            // Items.Add(new EddTreeViewItem(ProjectFile, Emd.GetEdd(2), 2));
            // Items.Add(new EmrTreeViewItem(ProjectFile, Emd.GetEmr(2), 2));
            if (Emd.Version == BioVersion.Biohazard2)
                Items.Add(new MeshTreeViewItem(ProjectFile, Emd.Md1));
            else
                Items.Add(new MeshTreeViewItem(ProjectFile, Emd.Md2));
        }
    }

    public class PlwTreeViewItem : ProjectTreeViewItem
    {
        public override string Header => ProjectFile.Filename;
        public PlwFile Plw { get; }

        public PlwTreeViewItem(ProjectFile projectFile)
            : base(projectFile)
        {
            Plw = (PlwFile)projectFile.Content;
            Items.Add(new EddTreeViewItem(ProjectFile, Plw.GetEdd(0), 0));
            Items.Add(new EmrTreeViewItem(ProjectFile, Plw.GetEmr(0), 0));
            if (Plw.Version == BioVersion.Biohazard2)
                Items.Add(new MeshTreeViewItem(ProjectFile, Plw.Md1));
            else
                Items.Add(new MeshTreeViewItem(ProjectFile, Plw.Md2));
            Items.Add(new TimTreeViewItem(ProjectFile, Plw.Tim));
        }
    }

    public class EddTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconEDD"];
        public override string Header => "EDD";
        public Edd Edd { get; }
        public int Index { get; }

        public EddTreeViewItem(ProjectFile projectFile, Edd edd, int index)
            : base(projectFile)
        {
            Edd = edd;
            Index = index;

            var numAnimations = edd.AnimationCount;
            for (var i = 0; i < numAnimations; i++)
            {
                Items.Add(new AnimationTreeViewItem(ProjectFile, edd, Index, i));
            }

            AddMenuItem("Import...", Import);
            AddMenuItem("Export...", Export);
        }

        private void Import()
        {
            CommonFileDialog
                .Open()
                .AddExtension("*.edd")
                .Show(path =>
                {
                    Model.SetEdd(0, new Edd(File.ReadAllBytes(path)));
                });
        }

        private void Export()
        {
            var dialog = CommonFileDialog.Save();
            if (ProjectFile != null)
            {
                dialog.WithDefaultFileName(Path.ChangeExtension(ProjectFile.Filename, ".EDD"));
            }
            dialog
                .AddExtension("*.edd")
                .Show(path => File.WriteAllBytes(path, Edd.GetBytes()));
        }
    }

    public class AnimationTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconAnimation"];
        public override string Header => $"Animation {Index}";
        public Edd Edd { get; }
        public int EddIndex { get; }
        public int Index { get; }

        public AnimationTreeViewItem(ProjectFile projectFile, Edd edd, int eddIndex, int index)
            : base(projectFile)
        {
            Edd = edd;
            EddIndex = eddIndex;
            Index = index;
        }

        public override void OnDefaultAction()
        {
            if (Model.Version == BioVersion.Biohazard3)
                return;

            var mainWindow = MainWindow.Instance;
            var project = mainWindow.Project;
            var emr = Model.GetEmr(EddIndex);
            if (ProjectFile.Content is PldFile pldFile)
            {
                mainWindow.LoadModel(pldFile, pldFile.Tim);
                mainWindow.LoadAnimation(emr, Edd, Index);
            }
            else if (ProjectFile.Content is PlwFile plwFile)
            {
                if (project.MainModel is PldFile parentPldFile)
                {
                    mainWindow.LoadModel(parentPldFile, parentPldFile.Tim);
                    mainWindow.LoadWeaponModel(plwFile);
                    mainWindow.LoadAnimation(emr, Edd, Index);
                }
            }
            else if (ProjectFile.Content is EmdFile emdFile)
            {
                mainWindow.LoadModel(emdFile, MainWindow.Instance.Project.MainTexture);
                mainWindow.LoadAnimation(emr, Edd, Index);
            }
        }
    }

    public class EmrTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconEMR"];
        public override string Header => "EMR";
        public Emr Emr { get; }
        public int EmrIndex { get; }

        public EmrTreeViewItem(ProjectFile projectFile, Emr emr, int emrIndex)
            : base(projectFile)
        {
            Emr = emr;
            EmrIndex = emrIndex;
            if (emr.NumParts > 0 && !(Model is PlwFile))
            {
                Items.Add(new ArmatureTreeViewItem(ProjectFile, emr, emrIndex, 0));
            }

            AddMenuItem("Import...", Import);
            AddMenuItem("Export...", Export);
        }

        public override void OnSelect()
        {
            MainWindow.Instance.SelectPart(-1);
        }

        public override void OnDefaultAction()
        {
            if (ProjectFile.Content is PldFile pldFile)
            {
                MainWindow.Instance.LoadModel(pldFile, pldFile.Tim);
            }
        }

        private void Import()
        {
            CommonFileDialog
                .Open()
                .AddExtension("*.emr")
                .Show(path =>
                {
                    Model.SetEmr(0, new Emr(File.ReadAllBytes(path)));
                });
        }

        private void Export()
        {
            var dialog = CommonFileDialog.Save();
            if (ProjectFile != null)
            {
                dialog.WithDefaultFileName(Path.ChangeExtension(ProjectFile.Filename, ".EMR"));
            }
            dialog
                .AddExtension("*.emr")
                .Show(path => File.WriteAllBytes(path, Emr.GetBytes()));
        }
    }

    public class ArmatureTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconArmature"];
        public Emr Emr { get; }
        public int EmrIndex { get; }
        public int PartIndex { get; }

        public ArmatureTreeViewItem(ProjectFile projectFile, Emr emr, int emrIndex, int partIndex)
            : base(projectFile)
        {
            Emr = emr;
            EmrIndex = emrIndex;
            PartIndex = partIndex;

            var children = Emr.GetArmatureParts(partIndex);
            foreach (var child in children)
            {
                Items.Add(new ArmatureTreeViewItem(ProjectFile, emr, emrIndex, child));
            }
        }

        public override void OnSelect()
        {
            MainWindow.Instance.SelectPart(PartIndex);
        }

        public override void OnDefaultAction()
        {
            if (ProjectFile.Content is PldFile pldFile)
            {
                MainWindow.Instance.LoadModel(pldFile, pldFile.Tim);
                MainWindow.Instance.SelectPart(PartIndex);
            }
        }

        public override string Header
        {
            get
            {
                var partIndex = PartIndex;
                if (Model.Version == BioVersion.Biohazard2)
                {
                    if (g_partNamesRe2.Length > partIndex)
                        return g_partNamesRe2[partIndex];
                }
                else
                {
                    if (g_partNamesRe3.Length > partIndex)
                        return g_partNamesRe3[partIndex];
                }
                return $"Part {partIndex}";
            }
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
    }

    public class MeshTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconMD1"];
        public override string Header => Mesh.Version == BioVersion.Biohazard2 ? "MD1" : "MD2";
        public IModelMesh Mesh { get; private set; }

        public MeshTreeViewItem(ProjectFile projectFile, IModelMesh mesh)
            : base(projectFile)
        {
            Mesh = mesh;
            CreateChildren();
            AddMenuItem("Import...", Import);
            AddMenuItem("Export...", Export);
            AddSeperator();
            AddMenuItem("Add part", AddPart);
            if (Model is EmdFile && mesh.Version == BioVersion.Biohazard3)
            {
                AddMenuItem("Copy hand to gun hand", AutoHandWithGun);
            }
        }

        private string DefaultExtension => Mesh.Version == BioVersion.Biohazard2 ? ".MD1" : ".MD2";
        private string ExtensionPattern => Mesh.Version == BioVersion.Biohazard2 ? "*.md1" : "*.md2";

        private void CreateChildren()
        {
            Items.Clear();
            for (var i = 0; i < Mesh.NumParts; i++)
            {
                Items.Add(new MeshPartTreeViewItem(ProjectFile, Mesh, i));
            }
        }

        public override void OnSelect()
        {
            MainWindow.Instance.SelectPart(-1);
        }

        public override void OnDefaultAction()
        {
            MainWindow.Instance.LoadModel(Model, MainWindow.Instance.Project.MainTexture);
        }

        private void Import()
        {
            CommonFileDialog
                .Open()
                .AddExtension(ExtensionPattern)
                .AddExtension("*.emd")
                .AddExtension("*.pld")
                .AddExtension("*.obj")
                .Show(path =>
                {
                    if (path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    {
                        ImportFromObj(path);
                    }
                    else if (path.EndsWith(".emd", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pld", StringComparison.OrdinalIgnoreCase))
                    {
                        ImportFromModel(path);
                    }
                    else
                    {
                        if (Model.Version == BioVersion.Biohazard2)
                            Mesh = Model.Md1 = new Md1(File.ReadAllBytes(path));
                        else
                            Mesh = Model.Md2 = new Md2(File.ReadAllBytes(path));
                    }
                    CreateChildren();
                    MainWindow.Instance.LoadModel(Model, MainWindow.Instance.Project.MainTexture);
                });
        }

        private void ImportFromObj(string path)
        {
            var project = MainWindow.Instance.Project;
            var tim = project.MainTexture;
            var emr = Model.GetEmr(0);
            var numPages = tim.Width / 128;
            var objImporter = new ObjImporter();
            Mesh = Model.Md1 = objImporter.ImportMd1(path, numPages, emr.GetFinalPosition);
        }

        private void ImportFromModel(string path)
        {
            var project = MainWindow.Instance.Project;
            var modelFile = ModelFile.FromFile(path);
            if (modelFile.Version == BioVersion.Biohazard2)
            {
                if (Model.Version == BioVersion.Biohazard2)
                {
                    Model.Md1 = modelFile.Md1;
                    Model.SetEmr(0, modelFile.GetEmr(0));
                }
                else
                {
                    Model.Md2 = modelFile.Md1.ToMd2();

                    var map2to3 = new[]
                    {
                        0, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7
                    };
                    var emr = modelFile.GetEmr(0);
                    var emrBuilder = Model.GetEmr(0).ToBuilder();
                    for (var i = 0; i < map2to3.Length; i++)
                    {
                        var srcPartIndex = i;
                        var dstPartIndex = map2to3[i];
                        var src = emr.GetRelativePosition(srcPartIndex);
                        emrBuilder.RelativePositions[dstPartIndex] = src;
                    }
                    Model.SetEmr(0, emrBuilder.ToEmr());
                }
            }
            else
            {
                if (Model.Version == BioVersion.Biohazard2)
                {
                    Model.Md1 = modelFile.Md2.ToMd1();
                }
                else
                {
                    Model.Md2 = modelFile.Md2;

                    var emr = modelFile.GetEmr(0);
                    var emrBuilder = Model.GetEmr(0).ToBuilder();
                    for (var i = 0; i < 15; i++)
                    {
                        emrBuilder.RelativePositions[i] = emr.GetRelativePosition(i);
                    }
                    Model.SetEmr(0, emrBuilder.ToEmr());
                }
            }

            if (modelFile is PldFile pldFile)
            {
                project.MainTexture = pldFile.Tim;
            }
            else
            {
                var timPath = Path.ChangeExtension(path, ".tim");
                if (File.Exists(timPath))
                {
                    project.MainTexture = new TimFile(timPath);
                }
            }
        }

        private void Export()
        {
            var dialog = CommonFileDialog.Save();
            if (ProjectFile != null)
            {
                dialog.WithDefaultFileName(Path.ChangeExtension(ProjectFile.Filename, DefaultExtension));
            }
            dialog
                .AddExtension(ExtensionPattern)
                .AddExtension("*.obj")
                .Show(path =>
                {
                    if (path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    {
                        var project = MainWindow.Instance.Project;
                        var tim = project.MainTexture;
                        var emr = Model.GetEmr(0);
                        if (Mesh is Md1 md1)
                        {
                            var numPages = tim.Width / 128;
                            var objExporter = new ObjExporter();
                            objExporter.Export(Model.Md1, path, numPages, emr.GetFinalPosition);

                            var texturePath = Path.ChangeExtension(path, ".png");
                            tim.ToBitmap().Save(texturePath);
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(path, Mesh.GetBytes());
                    }
                });
        }

        private void AddPart()
        {
            if (Mesh is Md1 md1)
            {
                var part = new Md1Builder.Part();
                part.Positions.Add(new Md1.Vector());
                part.Normals.Add(new Md1.Vector());
                part.Triangles.Add(new Md1.Triangle());
                part.TriangleTextures.Add(new Md1.TriangleTexture());

                var md1Builder = md1.ToBuilder();
                md1Builder.Parts.Add(part);
                Mesh = Model.Md1 = md1Builder.ToMd1();
            }
            else if (Mesh is Md2 md2)
            {
                var md2Builder = md2.ToBuilder();
                md2Builder.Parts.Add(new Md2Builder.Part());
                Mesh = Model.Md2 = md2Builder.ToMd2();
            }
            CreateChildren();
        }

        private void AutoHandWithGun()
        {
            var builder = Model.Md2.ToBuilder();
            if (builder.Parts.Count < 16)
            {
                var part = new Md2Builder.Part();
                part.Positions.Add(new Md2.Vector());
                part.Normals.Add(new Md2.Vector());
                part.Triangles.Add(new Md2.Triangle());
                builder.Parts.Add(part);
            }
            builder.Parts[15] = builder.Parts[4];
            Model.Md2 = builder.ToMd2();
            CreateChildren();
        }
    }

    public class MeshPartTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconPart"];
        public override string Header => $"Part {PartIndex}";
        public IModelMesh Mesh { get; }
        public int PartIndex { get; }

        private string DefaultExtension => Mesh.Version == BioVersion.Biohazard2 ? ".MD1" : ".MD2";
        private string ExtensionPattern => Mesh.Version == BioVersion.Biohazard2 ? "*.md1" : "*.md2";

        public MeshPartTreeViewItem(ProjectFile projectFile, IModelMesh mesh, int partIndex)
            : base(projectFile)
        {
            Mesh = mesh;
            PartIndex = partIndex;

            AddMenuItem("Import...", Import);
            AddMenuItem("Export...", Export);
        }

        public override void OnSelect()
        {
            MainWindow.Instance.SelectPart(PartIndex);
        }

        public override void OnDefaultAction()
        {
            var mainWindow = MainWindow.Instance;
            var project = mainWindow.Project;
            var projectFile = ProjectFile;
            if (projectFile.Content is PldFile pldFile)
            {
                if (project.Version == BioVersion.Biohazard2)
                {
                    var builder = pldFile.Md1.ToBuilder();
                    var partMd1 = builder.Parts[PartIndex];
                    builder.Parts.Clear();
                    builder.Parts.Add(partMd1);
                    var singleMd1 = builder.ToMd1();
                    mainWindow.LoadIsolatedModel(singleMd1, pldFile.Tim, PartIndex);
                }
                else
                {
                    var builder = pldFile.Md2.ToBuilder();
                    var partMd2 = builder.Parts[PartIndex];
                    builder.Parts.Clear();
                    builder.Parts.Add(partMd2);
                    var singleMd2 = builder.ToMd2();
                    mainWindow.LoadIsolatedModel(singleMd2, pldFile.Tim, PartIndex);
                }
            }
            else if (projectFile.Content is PlwFile plwFile)
            {
                if (project.MainModel is PldFile parentPldFile)
                {
                    var tim = parentPldFile.Tim;
                    var plwTim = plwFile.Tim;
                    for (var y = 0; y < 32; y++)
                    {
                        for (var x = 0; x < 56; x++)
                        {
                            var p = plwTim.GetPixel(x, y);
                            tim.SetPixel(200 + x, 224 + y, 1, p);
                        }
                    }

                    var builder = plwFile.Md1.ToBuilder();
                    var partMd1 = builder.Parts[PartIndex];
                    builder.Parts.Clear();
                    builder.Parts.Add(partMd1);
                    var singleMd1 = builder.ToMd1();
                    mainWindow.LoadIsolatedModel(singleMd1, tim, PartIndex);
                }
            }
            else if (projectFile.Content is EmdFile emdFile)
            {
                var tim = MainWindow.Instance.Project.MainTexture;
                if (project.Version == BioVersion.Biohazard2)
                {
                    var builder = emdFile.Md1.ToBuilder();
                    var partMd1 = builder.Parts[PartIndex];
                    builder.Parts.Clear();
                    builder.Parts.Add(partMd1);
                    var singleMd1 = builder.ToMd1();
                    mainWindow.LoadIsolatedModel(singleMd1, tim, PartIndex);
                }
                else
                {
                    var builder = emdFile.Md2.ToBuilder();
                    var partMd2 = builder.Parts[PartIndex];
                    builder.Parts.Clear();
                    builder.Parts.Add(partMd2);
                    var singleMd2 = builder.ToMd2();
                    mainWindow.LoadIsolatedModel(singleMd2, tim, PartIndex);
                }
            }
        }

        private void Import()
        {
            CommonFileDialog
                .Open()
                .AddExtension(ExtensionPattern)
                .Show(path =>
                {
                    if (Model.Version == BioVersion.Biohazard2)
                    {
                        var srcBuilder = new Md1(File.ReadAllBytes(path)).ToBuilder();
                        if (srcBuilder.Parts.Count > 0)
                        {
                            var builder = Model.Md1.ToBuilder();
                            builder.Parts[PartIndex] = srcBuilder.Parts[0];
                            Model.Md1 = builder.ToMd1();
                        }
                    }
                    else
                    {
                        var srcBuilder = new Md2(File.ReadAllBytes(path)).ToBuilder();
                        if (srcBuilder.Parts.Count > 0)
                        {
                            var builder = Model.Md2.ToBuilder();
                            builder.Parts[PartIndex] = srcBuilder.Parts[0];
                            Model.Md2 = builder.ToMd2();
                        }
                    }
                    MainWindow.Instance.LoadModel(Model, MainWindow.Instance.Project.MainTexture);
                });
        }

        private void Export()
        {
            var dialog = CommonFileDialog.Save();
            if (ProjectFile != null)
            {
                dialog.WithDefaultFileName(Path.ChangeExtension(ProjectFile.Filename, DefaultExtension));
            }
            dialog
                .AddExtension(ExtensionPattern)
                .Show(path =>
                {
                    if (Model.Version == BioVersion.Biohazard2)
                    {
                        var builder = Model.Md1.ToBuilder();
                        var interestedPart = builder.Parts[PartIndex];
                        builder.Parts.Clear();
                        builder.Parts.Add(interestedPart);
                        File.WriteAllBytes(path, builder.ToMd1().GetBytes());
                    }
                    else
                    {
                        var builder = Model.Md2.ToBuilder();
                        var interestedPart = builder.Parts[PartIndex];
                        builder.Parts.Clear();
                        builder.Parts.Add(interestedPart);
                        File.WriteAllBytes(path, builder.ToMd2().GetBytes());
                    }
                });
        }
    }

    public class TimTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconTIM"];
        public override string Header => ProjectFile.Kind == ProjectFileKind.Tim ? ProjectFile.Filename : "TIM";
        public TimFile Tim { get; }

        public TimTreeViewItem(ProjectFile projectFile, TimFile tim)
            : base(projectFile)
        {
            Tim = tim;

            AddMenuItem("Import...", Import);
            AddMenuItem("Export...", Export);
        }

        private void Import()
        {
            CommonFileDialog
                .Open()
                .AddExtension("*.tim")
                .Show(path =>
                {
                    if (Model is PldFile pld)
                    {
                        pld.Tim = new TimFile(path);
                    }
                    else if (Model is PlwFile plw)
                    {
                        plw.Tim = new TimFile(path);
                    }
                    else if (ProjectFile.Kind == ProjectFileKind.Tim)
                    {
                        MainWindow.Instance.Project.MainTexture = new TimFile(path);
                    }
                    MainWindow.Instance.LoadModel(Model, MainWindow.Instance.Project.MainTexture);
                });
        }

        private void Export()
        {
            var dialog = CommonFileDialog.Save();
            if (ProjectFile != null)
            {
                dialog.WithDefaultFileName(Path.ChangeExtension(ProjectFile.Filename, ".TIM"));
            }
            dialog
                .AddExtension("*.png")
                .AddExtension("*.tim")
                .Show(path =>
                {
                    if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        Tim.ToBitmap().Save(path);
                    }
                    else
                    {
                        Tim.Save(path);
                    }
                });
        }
    }
}
