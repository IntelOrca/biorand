using System;
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
            treeView.Items.Clear();
            if (_project == null)
                return;

            foreach (var projectFile in _project.Files)
            {
                CreateItem(treeView, projectFile);
            }
        }

        private TreeViewItem CreateItem(object parent, object item)
        {
            var tvItem = new TreeViewItem();
            if (parent is TreeView tv)
                tv.Items.Add(tvItem);
            else if (parent is TreeViewItem tvi)
                tvi.Items.Add(tvItem);

            tvItem.Tag = item;
            if (item is ProjectFile projectFile)
            {
                tvItem.Header = new ProjectTreeViewItemHeader(GetImageSource(projectFile.Kind), projectFile.Filename);
                if (projectFile.Content is ModelFile modelFile)
                {
                    CreateItem(tvItem, modelFile.GetEdd(0));
                    CreateItem(tvItem, modelFile.GetEmr(0));
                    CreateItem(tvItem, modelFile.Md1);
                    if (modelFile is PldFile pldFile)
                    {
                        CreateItem(tvItem, pldFile.GetTim());
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
                CreateItem(tvItem, new Armature(emr, 0));
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

        private ImageSource GetImage(string key) => (ImageSource)Resources[key];

        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
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
            var tvi = treeView.SelectedItem as TreeViewItem;
            while (tvi != null)
            {
                if (tvi.Tag is ProjectFile projectFile)
                {
                    return projectFile;
                }
                tvi = tvi.Parent as TreeViewItem;
            }
            return null;
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
                    var builder = pldFile.Md1.ToBuilder();
                    var partMd1 = builder.Parts[part.Index];
                    builder.Parts.Clear();
                    builder.Parts.Add(partMd1);
                    var singleMd1 = builder.ToMd1();
                    MainWindow.LoadIsolatedModel(singleMd1, pldFile.Tim);
                    e.Handled = true;
                }
                else if (projectFile.Content is PlwFile plwFile)
                {
                    if (_project.MainModel is PldFile parentPldFile)
                    {
                        var tim = parentPldFile.GetTim();
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
            else if (item is Md1 || item is Emr || item is Armature)
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
            if (item is Edd edd)
            {
                dialog
                    .AddExtension("*.edd")
                    .Show(path => File.ReadAllBytes(path));
            }
            else if (item is Emr emr)
            {
                dialog
                    .AddExtension("*.emr")
                    .Show(path => File.ReadAllBytes(path));
            }
            else if (item is Md1 md1)
            {
                dialog
                    .AddExtension("*.md1")
                    .Show(path => File.ReadAllBytes(path));
            }
            else if (item is TimFile tim)
            {
                dialog
                    .AddExtension("*.png")
                    .AddExtension("*.tim")
                    .Show(path => { });
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
}
