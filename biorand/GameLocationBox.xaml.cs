using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for GameLocationBox.xaml
    /// </summary>
    public partial class GameLocationBox : UserControl
    {
        public event EventHandler Changed;
        public event EventHandler<PathValidateEventArgs> Validate;

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(GameLocationBox));

        public static readonly DependencyProperty LocationProperty =
            DependencyProperty.Register(nameof(Location), typeof(string), typeof(GameLocationBox));

        /// <summary>
        /// Flag to know if the settings are completely loaded. Used to avoid triggering a save when we are loading the configs.
        /// </summary>
        public bool IsSettingsLoaded { get; set; }

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string Location
        {
            get => (string)GetValue(LocationProperty);
            set => SetValue(LocationProperty, value);
        }

        public bool? IsChecked
        {
            get => groupBox.IsChecked;
            set => groupBox.IsChecked = value;
        }

        public string SelectedExecutable
        {
            get => cbExecutables.SelectedItem?.ToString();
        }

        public GameLocationBox()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = $"Select {Header} / Biohazard Game Location";
            if (Directory.Exists(txtGameDataLocation.Text))
                dialog.InitialDirectory = txtGameDataLocation.Text;
            dialog.Filter = "Executable Files (*.exe)|*.exe";
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = false;
            var window = Window.GetWindow(this);
            if (dialog.ShowDialog(window) == true)
            {
                ValidatePath(dialog.FileName);
                SetExecutableList(Path.GetDirectoryName(dialog.FileName), Path.GetFileName(dialog.FileName));
            }
        }

        private void ValidatePath(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }

                var eventArgs = new PathValidateEventArgs();
                eventArgs.Path = path;
                Validate?.Invoke(this, eventArgs);

                txtValidationMessage.Text = eventArgs.Message;
                txtValidationMessage.Foreground = eventArgs.IsValid ?
                    Brushes.Green :
                    Brushes.Red;
            }
            catch (Exception ex)
            {
                txtValidationMessage.Text = ex.Message;
                txtValidationMessage.Foreground = Brushes.Red;
            }

            Location = path;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void txtGameDataLocation_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidatePath(txtGameDataLocation.Text);
        }

        /// <summary>
        /// Load the executables(exe) from the selected game directoy and set the default selection. 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="selection"></param>
        public void SetExecutableList(string directory, string selection)
        {
            if (!Directory.Exists(directory))
                return;

            cbExecutables.Items.Clear();
            foreach (var executable in Directory.GetFiles(directory, "*.exe"))
            {
                cbExecutables.Items.Add(Path.GetFileName(executable));
            }
            cbExecutables.SelectedItem = selection;
        }

        private void cbExecutables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(IsSettingsLoaded)
                Changed?.Invoke(this, EventArgs.Empty);
        }

        private void groupBox_OnCheckedChanged(object sender, EventArgs e)
        {
            if (IsSettingsLoaded)
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public class PathValidateEventArgs : EventArgs
    {
        public string Path { get; set; }
        public string Message { get; set; }
        public bool IsValid { get; set; }
    }
}
