using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for SeedQR.xaml
    /// </summary>
    public partial class SeedQR : UserControl
    {
        public static readonly DependencyProperty SeedProperty =
            DependencyProperty.Register(
                nameof(Seed),
                typeof(RandoConfig),
                typeof(SeedQR),
                new PropertyMetadata(Seed_Changed));

        public RandoConfig Seed
        {
            get => (RandoConfig)GetValue(SeedProperty);
            set => SetValue(SeedProperty, value);
        }

        public SeedQR()
        {
            InitializeComponent();
            UpdateImage();
        }

        private void UpdateImage()
        {
            var config = Seed;
            if (config == null)
            {
                config = new RandoConfig();
            }

            var seed = config.ToString();
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(seed, QRCodeGenerator.ECCLevel.M);
            var qrCode = new QRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(3);
            image.Source = ConvertBitmap(qrCodeImage);
            image.Stretch = Stretch.None;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        }

        private BitmapSource ConvertBitmap(System.Drawing.Bitmap bitmap)
        {
            var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.EndInit();
            return bi;
        }

        private static void Seed_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as SeedQR).UpdateImage();
        }
    }
}
