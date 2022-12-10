using System;
using System.IO;
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

        public GameLocationBox()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select Resident Evil 2 / Biohazard Game Location";
            if (Directory.Exists(txtGameDataLocation.Text))
                dialog.InitialDirectory = txtGameDataLocation.Text;
            dialog.Filter = "Executable Files (*.exe)|*.exe";
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = false;
            var window = Window.GetWindow(this);
            if (dialog.ShowDialog(window) == true)
            {
                ValidatePath(dialog.FileName);
            }
        }

        private void ValidatePath(string path)
        {
            if (File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            var eventArgs = new PathValidateEventArgs();
            eventArgs.Path = path;
            Validate?.Invoke(this, eventArgs);

            Location = path;
            txtValidationMessage.Text = eventArgs.Message;
            txtValidationMessage.Foreground = eventArgs.IsValid ?
                Brushes.Green :
                Brushes.Red;

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void txtGameDataLocation_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidatePath(txtGameDataLocation.Text);
        }
    }

    public class PathValidateEventArgs : EventArgs
    {
        public string Path { get; set; }
        public string Message { get; set; }
        public bool IsValid { get; set; }
    }
}
