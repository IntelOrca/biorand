using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            }
            treeView.ItemsSource = items;

            // treeView.Items.Clear();
            // if (_project == null)
            //     return;
            // 
            // foreach (var projectFile in _project.Files)
            // {
            //     CreateItem(treeView, projectFile);
            // }
        }

        private TreeViewItem CreateItem(ItemsControl parent, object item, int insertIndex = -1)
        {
            var tvItem = new TreeViewItem();
            if (insertIndex == -1)
                parent.Items.Add(tvItem);
            else
                parent.Items.Insert(insertIndex, tvItem);

            tvItem.Tag = item;
            if (item is ProjectFile projectFile)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImageSource(projectFile.Kind), projectFile.Filename);
                if (projectFile.Content is ModelFile modelFile)
                {
                    CreateItem(tvItem, modelFile.GetEdd(0));
                    CreateItem(tvItem, modelFile.GetEmr(0));
                    if (modelFile.Version == BioVersion.Biohazard2)
                        CreateItem(tvItem, modelFile.Md1);
                    else
                        CreateItem(tvItem, modelFile.Md2);
                    if (modelFile is PldFile pldFile)
                    {
                        CreateItem(tvItem, pldFile.Tim);
                    }
                    else if (modelFile is PlwFile plwFile)
                    {
                        CreateItem(tvItem, plwFile.Tim);
                    }
                }
            }
            else if (item is Md1 md1)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconMD1"), "MD1");
                var numParts = md1.NumObjects / 2;
                for (var i = 0; i < numParts; i++)
                {
                    CreateItem(tvItem, new Part(i));
                }
            }
            else if (item is Md2 md2)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconMD1"), "MD2");
                var numParts = md2.NumObjects;
                for (var i = 0; i < numParts; i++)
                {
                    CreateItem(tvItem, new Part(i));
                }
            }
            else if (item is Edd edd)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconEDD"), "EDD");
                for (var i = 0; i < edd.AnimationCount; i++)
                {
                    CreateItem(tvItem, new Animation(edd, i));
                }
            }
            else if (item is Emr emr)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconEMR"), "EMR");
                if (!(GetParentModel(parent) is PlwFile))
                {
                    CreateItem(tvItem, new Armature(emr, 0));
                }
            }
            else if (item is TimFile tim)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconTIM"), "TIM");
            }
            else if (item is Part part)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconPart"), $"Part {part.Index}");
            }
            else if (item is Armature armature)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconArmature"), GetPartName(armature.Index));
                var children = armature.Emr.GetArmatureParts(armature.Index);
                foreach (var child in children)
                {
                    CreateItem(tvItem, new Armature(armature.Emr, child));
                }
            }
            else if (item is Animation animation)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImage("IconAnimation"), $"Animation {animation.Index}");
            }
            return tvItem;
        }

        private ImageSource GetImageSource(ProjectFileKind kind)
        {
            switch (kind)
            {
                case ProjectFileKind.Tim:
                    return GetImage("IconTIM");
                case ProjectFileKind.Plw:
                    return GetImage("IconPLW");
                default:
                    return GetImage("IconPLD");
            }
        }

        private ImageSource GetImage(string key) => (ImageSource)App.Current.Resources[key];

        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }

        private ProjectFile GetProjectFile(ItemsControl itemsControl)
        {
            var tvi = itemsControl;
            while (tvi != null)
            {
                if (tvi.Tag is ProjectFile projectFile)
                {
                    return projectFile;
                }
                tvi = tvi.Parent as ItemsControl;
            }
            return null;
        }

        private ModelFile GetParentModel(ItemsControl treeViewItem)
        {
            if (GetProjectFile(treeViewItem) is ProjectFile projectFile)
            {
                return projectFile.Content as ModelFile;
            }
            return null;
        }

        private object GetSelectedItem()
        {
            if (treeView.SelectedItem is TreeViewItem tvi)
            {
                return tvi.Tag;
            }
            return null;
        }

        private ProjectFile GetSelectedProjectFile()
        {
            return GetProjectFile(treeView.SelectedItem as ItemsControl);
        }

        private ModelFile GetSelectedModel()
        {
            return GetSelectedProjectFile()?.Content as ModelFile;
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

        }

        private void treeView_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var projectFile = GetSelectedProjectFile();
            var item = GetSelectedItem();
            if (item is Animation animation)
            {
                if (projectFile.Content is PldFile pldFile)
                {
                    MainWindow.LoadModel(pldFile, pldFile.Tim);
                    MainWindow.LoadAnimation(animation.Index);
                }
                else if (projectFile.Content is PlwFile plwFile)
                {
                    if (_project.MainModel is PldFile parentPldFile)
                    {
                        MainWindow.LoadModel(parentPldFile, parentPldFile.Tim);
                        MainWindow.LoadWeaponModel(plwFile);
                        MainWindow.LoadAnimation(animation.Index);
                        e.Handled = true;
                    }
                }
            }
            else if (item is Part part)
            {
                if (projectFile.Content is PldFile pldFile)
                {
                    if (_project.Version == BioVersion.Biohazard2)
                    {
                        var builder = pldFile.Md1.ToBuilder();
                        var partMd1 = builder.Parts[part.Index];
                        builder.Parts.Clear();
                        builder.Parts.Add(partMd1);
                        var singleMd1 = builder.ToMd1();
                        MainWindow.LoadIsolatedModel(singleMd1, pldFile.Tim);
                    }
                    else
                    {
                        var builder = pldFile.Md2.ToBuilder();
                        var partMd2 = builder.Parts[part.Index];
                        builder.Parts.Clear();
                        builder.Parts.Add(partMd2);
                        var singleMd2 = builder.ToMd2();
                        MainWindow.LoadIsolatedModel(singleMd2, pldFile.Tim);
                    }
                    e.Handled = true;
                }
                else if (projectFile.Content is PlwFile plwFile)
                {
                    if (_project.MainModel is PldFile parentPldFile)
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
                        var partMd1 = builder.Parts[part.Index];
                        builder.Parts.Clear();
                        builder.Parts.Add(partMd1);
                        var singleMd1 = builder.ToMd1();
                        MainWindow.LoadIsolatedModel(singleMd1, tim);
                        e.Handled = true;
                    }
                }
            }
            else if (item is Md1 || item is Md2 || item is Emr || item is Armature)
            {
                if (projectFile.Content is PldFile pldFile)
                {
                    MainWindow.LoadModel(pldFile, pldFile.Tim);
                    e.Handled = true;
                }
            }
        }

        private void importMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var projectFile = GetSelectedProjectFile();
            var item = GetSelectedItem();
            var dialog = CommonFileDialog.Open();
            if (projectFile.Content is ModelFile model)
            {
                if (item is Edd edd)
                {
                    dialog
                        .AddExtension("*.edd")
                        .Show(path =>
                        {
                            model.SetEdd(0, new Edd(File.ReadAllBytes(path)));
                        });
                }
                else if (item is Emr emr)
                {
                    dialog
                        .AddExtension("*.emr")
                        .Show(path =>
                        {
                            model.SetEmr(0, new Emr(File.ReadAllBytes(path)));
                        });
                }
                else if (item is Md1 md1)
                {
                    dialog
                        .AddExtension("*.md1")
                        .Show(path =>
                        {
                            if (model is PldFile pld)
                            {
                                model.Md1 = new Md1(File.ReadAllBytes(path));
                                MainWindow.LoadModel(model, pld.Tim);
                            }
                        });
                }
                else if (item is Md2 md2)
                {
                    dialog
                        .AddExtension("*.md2")
                        .Show(path =>
                        {
                            if (model is PldFile pld)
                            {
                                model.Md2 = new Md2(File.ReadAllBytes(path));
                                MainWindow.LoadModel(model, pld.Tim);
                            }
                        });
                }
                else if (item is TimFile tim)
                {
                    dialog
                        .AddExtension("*.tim")
                        .Show(path =>
                        {
                            if (model is PldFile pld)
                            {
                                pld.Tim = new TimFile(path);
                                MainWindow.LoadModel(model, pld.Tim);
                            }
                        });
                }
                else if (item is Part part)
                {
                    var builder = model.Md2.ToBuilder();
                    model.Md2 = builder.ToMd2();
                }
            }
        }

        private void exportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var projectFile = GetSelectedProjectFile();
            var item = GetSelectedItem();
            var dialog = CommonFileDialog.Save();
            if (item is Edd edd)
            {
                if (projectFile != null)
                {
                    dialog.WithDefaultFileName(Path.ChangeExtension(projectFile.Filename, ".EDD"));
                }
                dialog
                    .AddExtension("*.edd")
                    .Show(path => File.ReadAllBytes(path));
            }
            else if (item is Emr emr)
            {
                if (projectFile != null)
                {
                    dialog.WithDefaultFileName(Path.ChangeExtension(projectFile.Filename, ".EMR"));
                }
                dialog
                    .AddExtension("*.emr")
                    .Show(path => File.ReadAllBytes(path));
            }
            else if (item is Md1 md1)
            {
                if (projectFile != null)
                {
                    dialog.WithDefaultFileName(Path.ChangeExtension(projectFile.Filename, ".MD1"));
                }
                dialog
                    .AddExtension("*.md1")
                    .Show(path => File.WriteAllBytes(path, md1.GetBytes()));
            }
            else if (item is Md2 md2)
            {
                if (projectFile != null)
                {
                    dialog.WithDefaultFileName(Path.ChangeExtension(projectFile.Filename, ".MD2"));
                }
                dialog
                    .AddExtension("*.md2")
                    .Show(path => File.WriteAllBytes(path, md2.GetBytes()));
            }
            else if (item is TimFile tim)
            {
                if (projectFile != null)
                {
                    dialog.WithDefaultFileName(Path.ChangeExtension(projectFile.Filename, ".TIM"));
                }
                dialog
                    .AddExtension("*.png")
                    .AddExtension("*.tim")
                    .Show(path =>
                    {
                        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            tim.ToBitmap().Save(path);
                        }
                        else
                        {
                            tim.Save(path);
                        }
                    });
            }
            else if (item is Part part)
            {
                if (projectFile.Content is ModelFile modelFile)
                {
                    if (projectFile != null)
                    {
                        dialog.WithDefaultFileName(Path.ChangeExtension(projectFile.Filename, $".{part.Index:00}.MD2"));
                    }
                    dialog
                        .AddExtension("*.md2")
                        .Show(path =>
                        {
                            var builder = modelFile.Md2.ToBuilder();
                            var interestedPart = builder.Parts[part.Index];
                            builder.Parts.Clear();
                            builder.Parts.Add(interestedPart);
                            File.WriteAllBytes(path, builder.ToMd2().GetBytes());
                        });
                }
            }
        }

        private void addPartMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GetSelectedItem();
            var selectedModel = GetSelectedModel();
            if (selectedItem is Md1 md1)
            {
                var part = new Md1Builder.Part();
                part.Positions.Add(new Md1.Vector());
                part.Normals.Add(new Md1.Vector());
                part.Triangles.Add(new Md1.Triangle());
                part.TriangleTextures.Add(new Md1.TriangleTexture());

                var md1Builder = md1.ToBuilder();
                md1Builder.Parts.Add(part);
                selectedModel.Md1 = md1Builder.ToMd1();

                RefreshItem(treeView.SelectedItem as TreeViewItem);
            }
            else if (selectedItem is Md2 md2)
            {
                var md2Builder = md2.ToBuilder();
                md2Builder.Parts.Add(new Md2Builder.Part());
                selectedModel.Md2 = md2Builder.ToMd2();

                RefreshItem(treeView.SelectedItem as TreeViewItem);
            }
        }

        private void RefreshItem(ItemsControl item)
        {
            var parent = item.Parent as ItemsControl;
            var content = item.Tag;
            if (content is Md2 md2)
            {
                var model = GetParentModel(item);
                var index = parent.Items.IndexOf(item);
                var refreshedNode = CreateItem(parent, model.Md2, index);
                parent.Items.RemoveAt(index + 1);
                refreshedNode.IsExpanded = true;
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

        private string GetPartName(int partIndex)
        {
            if (_project.Version == BioVersion.Biohazard2)
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

        public class ProjectTreeViewItemHeader
        {
            public ImageSource Image { get; set; }
            public string Text { get; set; }

            public ProjectTreeViewItemHeader(ImageSource image, string text)
            {
                Image = image;
                Text = text;
            }
        }

        public class Part
        {
            public int Index { get; }

            public Part(int index)
            {
                Index = index;
            }
        }

        public class Armature
        {
            public Emr Emr { get; }
            public int Index { get; }

            public Armature(Emr emr, int index)
            {
                Emr = emr;
                Index = index;
            }
        }

        public class Animation
        {
            public Edd Edd { get; }
            public int Index { get; }

            public Animation(Edd edd, int index)
            {
                Edd = edd;
                Index = index;
            }
        }
    }

    public abstract class ProjectTreeViewItem
    {
        public virtual ImageSource Image => (ImageSource)App.Current.Resources["IconPLD"];
        public abstract string Header { get; }
        public virtual ObservableCollection<ProjectTreeViewItem> Items { get; } = new ObservableCollection<ProjectTreeViewItem>();
        public ProjectFile ProjectFile { get; }
        public ModelFile Model => ProjectFile.Content as ModelFile;

        public ProjectTreeViewItem(ProjectFile projectFile)
        {
            ProjectFile = projectFile;
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
            Items.Add(new EddTreeViewItem(ProjectFile, Pld.GetEdd(0)));
            Items.Add(new EmrTreeViewItem(ProjectFile, Pld.GetEmr(0)));
            if (Pld.Version == BioVersion.Biohazard2)
                Items.Add(new MeshTreeViewItem(ProjectFile, Pld.Md1));
            else
                Items.Add(new MeshTreeViewItem(ProjectFile, Pld.Md2));
            Items.Add(new TimTreeViewItem(ProjectFile, Pld.Tim));
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
            Items.Add(new EddTreeViewItem(ProjectFile, Plw.GetEdd(0)));
            Items.Add(new EmrTreeViewItem(ProjectFile, Plw.GetEmr(0)));
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

        public EddTreeViewItem(ProjectFile projectFile, Edd edd)
            : base(projectFile)
        {
            Edd = edd;

            var numAnimations = edd.AnimationCount;
            for (var i = 0; i < numAnimations; i++)
            {
                Items.Add(new AnimationTreeViewItem(ProjectFile, edd, i));
            }
        }
    }

    public class AnimationTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconAnimation"];
        public override string Header => $"Animation {Index}";
        public Edd Edd { get; }
        public int Index { get; }

        public AnimationTreeViewItem(ProjectFile projectFile, Edd edd, int index)
            : base(projectFile)
        {
            Edd = edd;
            Index = index;
        }
    }

    public class EmrTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconEMR"];
        public override string Header => "EMR";
        public Emr Emr { get; }

        public EmrTreeViewItem(ProjectFile projectFile, Emr emr)
            : base(projectFile)
        {
            Emr = emr;
            if (emr.NumParts > 0 && !(Model is PlwFile))
            {
                Items.Add(new ArmatureTreeViewItem(ProjectFile, emr, 0));
            }
        }
    }

    public class ArmatureTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconArmature"];
        public Emr Emr { get; }
        public int PartIndex { get; }

        public ArmatureTreeViewItem(ProjectFile projectFile, Emr emr, int partIndex)
            : base(projectFile)
        {
            Emr = emr;
            PartIndex = partIndex;

            var children = Emr.GetArmatureParts(partIndex);
            foreach (var child in children)
            {
                Items.Add(new ArmatureTreeViewItem(ProjectFile, emr, child));
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
        public IModelMesh Mesh { get; }

        public MeshTreeViewItem(ProjectFile projectFile, IModelMesh mesh)
            : base(projectFile)
        {
            Mesh = mesh;
            for (var i = 0; i < mesh.NumParts; i++)
            {
                Items.Add(new MeshPartTreeViewItem(ProjectFile, mesh, i));
            }
        }
    }

    public class MeshPartTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconPart"];
        public override string Header => $"Part {PartIndex}";
        public IModelMesh Mesh { get; }
        public int PartIndex { get; }

        public MeshPartTreeViewItem(ProjectFile projectFile, IModelMesh mesh, int partIndex)
            : base(projectFile)
        {
            Mesh = mesh;
            PartIndex = partIndex;
        }
    }

    public class TimTreeViewItem : ProjectTreeViewItem
    {
        public override ImageSource Image => (ImageSource)Application.Current.Resources["IconTIM"];
        public override string Header => "TIM";
        public TimFile Tim { get; }

        public TimTreeViewItem(ProjectFile projectFile, TimFile tim)
            : base(projectFile)
        {
            Tim = tim;
        }
    }
}
