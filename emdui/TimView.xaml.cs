using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using emdui.Extensions;
using IntelOrca.Biohazard;
using Microsoft.Win32;

namespace emdui
{
    /// <summary>
    /// Interaction logic for TimView.xaml
    /// </summary>
    public partial class TimView : UserControl
    {
        public event EventHandler TimUpdated;

        private TimFile _timFile;
        private int _selectedPage;

        public TimFile Tim
        {
            get => _timFile;
            set
            {
                if (_timFile != value)
                {
                    _timFile = value;
                    RefreshImage();
                }
            }
        }

        public int SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (_selectedPage != value)
                {
                    _selectedPage = value;
                    RefreshPage();
                }
            }
        }

        public TimView()
        {
            InitializeComponent();
            RefreshPage();
        }

        private void RefreshPage()
        {
            var borders = selectionContainer.Children.OfType<Border>().ToArray();
            for (var i = 0; i < borders.Length; i++)
            {
                var border = borders[i];
                border.Visibility = i == _selectedPage ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public void RefreshImage()
        {
            image.Width = _timFile.Width;
            image.Height = _timFile.Height;
            image.Source = _timFile.ToBitmap();
        }

        private void TimView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TimView_MouseMove(sender, e);
        }

        private void TimView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed ||
                e.RightButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(this);
                var pageWidth = ActualWidth / 4;
                var page = (int)(position.X / pageWidth);
                SelectedPage = page;
            }
        }

        private void RefreshAndRaiseTimEvent()
        {
            RefreshImage();
            TimUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureSelectedPageExists()
        {
            var minWidth = (_selectedPage + 1) * 128;
            if (_timFile.Width < minWidth)
            {
                _timFile.ResizeImage(minWidth, _timFile.Height);
            }
        }

        private void ImportPage(int page, TimFile source)
        {
            EnsureSelectedPageExists();
            var palette = source.GetPalette(0);
            _timFile.SetPalette(_selectedPage, palette);
            var dstX = page * 128;
            for (var y = 0; y < 256; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var p = source.GetRawPixel(x, y);
                    _timFile.SetRawPixel(dstX + x, y, p);
                }
            }
            RefreshAndRaiseTimEvent();
        }

        private TimFile ExtractPage(int page)
        {
            var palette = _timFile.GetPalette(page);
            var timFile = new TimFile(128, 256, 8);
            timFile.SetPalette(0, palette);
            var srcX = page * 128;
            for (var y = 0; y < 256; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var p = _timFile.GetRawPixel(srcX + x, y);
                    timFile.SetRawPixel(x, y, p);
                }
            }
            return timFile;
        }

        private void ImportPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "All Supported Files (*.png;*.tim)|*.png;*tim";
                openFileDialog.Filter += "|PNG (*.png)|*.png";
                openFileDialog.Filter += "|TIM (*.tim)|*.tim";
                if (openFileDialog.ShowDialog() == true)
                {
                    var path = openFileDialog.FileName;
                    if (path.EndsWith(".tim", StringComparison.OrdinalIgnoreCase))
                    {
                        var timFile = new TimFile(openFileDialog.FileName);
                        ImportPage(_selectedPage, timFile);
                    }
                    else
                    {
                        MessageBox.Show("Not yet implemented");
                    }
                }
            }
            catch
            {
            }
        }

        private void ExportPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var numPages = _timFile.Width / 128;
                if (numPages <= _selectedPage)
                    return;

                var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "TIM (*.tim)|*.tim";
                saveFileDialog.Filter += "|PNG (*.png)|*.png";
                if (saveFileDialog.ShowDialog() == true)
                {
                    var path = saveFileDialog.FileName;
                    var timFile = ExtractPage(_selectedPage);
                    if (path.EndsWith(".tim", StringComparison.OrdinalIgnoreCase))
                    {
                        timFile.Save(path);
                    }
                    else
                    {
                        timFile.ToBitmap().Save(path);
                    }
                }
            }
            catch
            {
            }
        }

        private void DeletePage_Click(object sender, RoutedEventArgs e)
        {
            var numPages = (_timFile.Width + 127) / 128;
            if (numPages > 1 && _selectedPage == numPages - 1)
            {
                _timFile.ResizeImage((numPages - 1) * 128, _timFile.Height);
            }
            else
            {
                var xStart = _selectedPage * 128;
                for (var y = 0; y < _timFile.Height; y++)
                {
                    for (var x = 0; x < 128; x++)
                    {
                        _timFile.SetRawPixel(xStart + x, y, 0);
                    }
                }
            }
            RefreshAndRaiseTimEvent();
        }

        private void CopyPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var numPages = _timFile.Width / 128;
                if (numPages <= _selectedPage)
                    return;

                var palette = _timFile.GetPalette(_selectedPage);
                var pixels = new ushort[128 * 256];
                var xStart = _selectedPage * 128;
                for (var y = 0; y < _timFile.Height; y++)
                {
                    for (var x = 0; x < 128; x++)
                    {
                        pixels[(y * 128) + x] = _timFile.GetRawPixel(xStart + x, y);
                    }
                }
                Clipboard.SetData(PageClipboardObject.Format, new PageClipboardObject(palette, pixels));
            }
            catch
            {
            }
        }

        private void PastePaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clipboardObject = Clipboard.GetData(PageClipboardObject.Format) as PageClipboardObject;
                if (clipboardObject == null)
                    return;

                EnsureSelectedPageExists();

                _timFile.SetPalette(_selectedPage, clipboardObject.Palette);
                var pixels = clipboardObject.Pixels;
                var xStart = _selectedPage * 128;
                for (var y = 0; y < _timFile.Height; y++)
                {
                    for (var x = 0; x < 128; x++)
                    {
                        _timFile.SetRawPixel(xStart + x, y, pixels[(y * 128) + x]);
                    }
                }
                RefreshAndRaiseTimEvent();
            }
            catch
            {
            }
        }

        [Serializable]
        private sealed class PageClipboardObject
        {
            public const string Format = "emdui_TIM_PAGE";

            public ushort[] Palette { get; }
            public ushort[] Pixels { get; }

            public PageClipboardObject(ushort[] palette, ushort[] pixels)
            {
                Palette = palette;
                Pixels = pixels;
            }
        }
    }
}
