using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Version CurrentVersion = Assembly.GetEntryAssembly().GetName().Version;

        private Random _random = new Random();
        private RandoAppSettings _settings = new RandoAppSettings();
        private RandoConfig _config = new RandoConfig();
        private bool _suspendEvents;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEvents();
            LoadSettings();
            UpdateUi();
            UpdateEnabledUi();
#if !DEBUG
            CheckForNewVersion();
#endif

            var version = CurrentVersion;
            Title += $" - v{version.Major}.{version.Minor}.{version.Build}";
        }

        private void LoadSettings()
        {
            _settings = RandoAppSettings.Load();
            _config = _settings.Seed == null ? new RandoConfig() : RandoConfig.FromString(_settings.Seed);
            if (_settings.Seed == null)
            {
                RandomizeSeed();
            }
            txtGameDataLocation.Text = _settings.GamePath;
        }

        private void SaveSettings()
        {
            _settings.GamePath = txtGameDataLocation.Text;
            _settings.Seed = _config.ToString();
            _settings.Save();
        }

        private void InitializeEvents()
        {
            foreach (var control in GetAllControls(this))
            {
                if (control is CheckBox cb)
                {
                    cb.Unchecked += OnCheckBoxChanged;
                    cb.Checked += OnCheckBoxChanged;
                }
                else if (control is CheckGroupBox cgb)
                {
                    cgb.Unchecked += OnCheckBoxChanged;
                    cgb.Checked += OnCheckBoxChanged;
                }
                else if (control is Slider slider)
                {
                    slider.ValueChanged += Slider_ValueChanged;
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.SelectionChanged += ComboBox_SelectionChanged;
                }
            }
        }

        private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!_suspendEvents)
            {
                UpdateConfig();
                UpdateUi();
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                slider.Value = (byte)slider.Value;
            }

            if (!_suspendEvents)
            {
                UpdateConfig();
                UpdateUi();
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suspendEvents)
            {
                UpdateConfig();
                UpdateUi();
            }
        }

        private void UpdateUi()
        {
            try
            {
                _suspendEvents = true;

                chkRngDoors.IsChecked = _config.RandomDoors;
                chkRngChars.IsChecked = _config.RandomNPCs;
                chkRngBgm.IsChecked = _config.RandomBgm;
                chkProtectSoftLock.IsChecked = _config.ProtectFromSoftLock || _config.RandomDoors;
                chkRngEnemies.IsChecked = _config.RandomEnemies;
                chkRngItems.IsChecked = _config.RandomItems || _config.RandomDoors;
                chkShuffleItems.IsChecked = _config.ShuffleItems && !_config.RandomDoors;
                chkAlternativeRoute.IsChecked = _config.AlternativeRoutes && !_config.RandomDoors;
                chkIncludeDocuments.IsChecked = _config.IncludeDocuments;

                sliderEnemyDifficulty.Value = _config.EnemyDifficulty;

                sliderAmmo.Value = _config.RatioAmmo;
                sliderHealth.Value = _config.RatioHealth;
                sliderInkRibbons.Value = _config.RatioInkRibbons;
                sliderAmmoQuantity.Value = _config.AmmoQuantity;

                sliderAreaCount.Value = _config.AreaCount;
                sliderAreaSize.Value = _config.AreaSize;

                dropdownVariant.SelectedIndex = _config.GameVariant;

                txtSeed.Text = _config.ToString();
            }
            finally
            {
                _suspendEvents = false;
            }

            UpdateEnabledUi();
        }

        private void UpdateConfig()
        {
            _config.RandomDoors = chkRngDoors.IsChecked == true;
            _config.RandomNPCs = chkRngChars.IsChecked == true;
            _config.RandomBgm = chkRngBgm.IsChecked == true;
            _config.ProtectFromSoftLock = chkProtectSoftLock.IsChecked == true || _config.RandomDoors;
            _config.RandomEnemies = chkRngEnemies.IsChecked == true;
            _config.RandomItems = chkRngItems.IsChecked == true || _config.RandomDoors;
            _config.ShuffleItems = chkShuffleItems.IsChecked == true && !_config.RandomDoors;
            _config.AlternativeRoutes = chkAlternativeRoute.IsChecked == true && !_config.RandomDoors;
            _config.IncludeDocuments = chkIncludeDocuments.IsChecked == true;

            _config.EnemyDifficulty = (byte)sliderEnemyDifficulty.Value;

            _config.RatioAmmo = (byte)sliderAmmo.Value;
            _config.RatioHealth = (byte)sliderHealth.Value;
            _config.RatioInkRibbons = (byte)sliderInkRibbons.Value;
            _config.AmmoQuantity = (byte)sliderAmmoQuantity.Value;

            _config.AreaCount = (byte)sliderAreaCount.Value;
            _config.AreaSize = (byte)sliderAreaSize.Value;

            _config.GameVariant = (byte)dropdownVariant.SelectedIndex;

            SaveSettings();
        }

        private void UpdateEnabledUi()
        {
            panelItemSliders.IsEnabled = chkShuffleItems.IsChecked != true;
            chkShuffleItems.IsEnabled = chkRngDoors.IsChecked != true;
            chkAlternativeRoute.IsEnabled = chkRngDoors.IsChecked != true;
            chkProtectSoftLock.IsEnabled = chkRngDoors.IsChecked != true;
        }

        private IEnumerable<FrameworkElement> GetAllControls(object parent)
        {
            if (!(parent is FrameworkElement c))
                yield break;

            yield return c;

            if (parent is ContentControl cc)
            {
                foreach (var child in GetAllControls(cc.Content))
                {
                    yield return child;
                }
            }
            else if (parent is Panel p)
            {
                foreach (var child in p.Children)
                {
                    foreach (var grandChild in GetAllControls(child))
                    {
                        yield return grandChild;
                    }
                }
            }
        }

        private void RandomizeSeed_Click(object sender, RoutedEventArgs e)
        {
            RandomizeSeed();
            UpdateUi();
        }

        private void RandomizeSeed()
        {
            _config.Version = RandoConfig.LatestVersion;
            _config.Seed = _random.Next(0, RandoConfig.MaxSeed);
            SaveSettings();
        }

        private void RandomizeConfig_Click(object sender, RoutedEventArgs e)
        {
            _config.Version = RandoConfig.LatestVersion;
            _config.Seed = _random.Next(0, RandoConfig.MaxSeed);
            _config.EnemyDifficulty = (byte)_random.Next(0, 4);
            _config.AmmoQuantity = (byte)_random.Next(0, 8);
            _config.RatioAmmo = (byte)_random.Next(0, 32);
            _config.RatioHealth = (byte)_random.Next(0, 32);
            _config.RatioInkRibbons = (byte)_random.Next(0, 32);
            _config.ShuffleItems = false;
            _config.AreaCount = (byte)_random.Next(1, 4);
            SaveSettings();
            UpdateUi();
        }

        private void txtSeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suspendEvents)
                return;

            var txt = txtSeed.Text;
            foreach (var change in e.Changes)
            {
                if (change.AddedLength > 0 && change.RemovedLength > 0)
                {
                }
                else if (change.AddedLength > 0)
                {
                    if (change.Offset + 1 < txt.Length)
                    {
                        txt = txt.Remove(change.Offset + 1, 1);
                    }
                }
                else if (change.RemovedLength > 0)
                {
                    txt = txt.Insert(change.Offset, "0");
                }
            }

            _config = RandoConfig.FromString(txt);
            var caretIndex = txtSeed.CaretIndex;
            UpdateUi();
            txtSeed.Text = _config.ToString();
            txtSeed.CaretIndex = Math.Min(caretIndex, txtSeed.Text.Length);
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            var gamePath = txtGameDataLocation.Text;
            if (!Path.IsPathRooted(gamePath) || !Directory.Exists(gamePath))
            {
                var msg = "You have not specified an RE2 game directory that exists.";
                ShowGenerateFailedMessage(msg);
                return;
            }

            if (!ValidateGamePath(gamePath))
            {
                if (!ShowGamePathWarning())
                {
                    return;
                }
            }

            try
            {
                var err = Program.DoIntegrityCheck(gamePath);
                if (err == 2)
                {
                    ShowFailedMessage("Integrity Check Failed", "One or more of your room files are missing.\n" +
                        "Check that your RE2 installation is integral.");
                }
                else if (err == 1)
                {
                    ShowFailedMessage("Integrity Check Failed", "One or more of your room files did not match the original version.\n" +
                        "If you have installed over another mod, issues may occur so proceed with caution!");
                }
            }
            catch (Exception ex)
            {
                ShowFailedMessage("Failed to do integrity check!", ex.Message);
            }

            var btn = (Button)sender;
            try
            {
                btn.Content = "Generating...";
                IsEnabled = false;
                await Task.Run(() => Program.Generate(_config, gamePath));
                RandoAppSettings.LogGeneration(_config.ToString(), gamePath);
                ShowGenerateCompleteMessage();
            }
            catch (Exception ex)
            {
                ShowMessageForException(ex);
            }
            finally
            {
                btn.Content = "Generate";
                IsEnabled = true;
            }
        }

        private void ShowMessageForException(Exception ex)
        {
            var accessDeniedMessage =
                "BioRand does not have permission to write to this location.\n" +
                "If your game is installed in 'Program Files', you may need to run BioRand as administrator.";

            if (ex is AggregateException)
                ex = ex.InnerException;

            if (ex is UnauthorizedAccessException)
                ShowGenerateFailedMessage(accessDeniedMessage);
            else if (IsTypicalException(ex))
                ShowGenerateFailedMessage("An error occured during generation.\nPlease report this seed and try another.");
            else
                ShowGenerateFailedMessage(ex.Message + "\n" + "Please report this seed and try another.");
        }

        private static bool IsTypicalException(Exception ex)
        {
            if (ex is ArgumentOutOfRangeException) return true;
            if (ex is ArgumentNullException) return true;
            if (ex is ArgumentException) return true;
            if (ex is IndexOutOfRangeException) return true;
            if (ex is InvalidOperationException) return true;
            if (ex is NullReferenceException) return true;
            return false;
        }

        private void ShowGenerateFailedMessage(string message)
        {
            ShowFailedMessage("Failed to Generate", message);
        }

        private void ShowFailedMessage(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select Resident Evil 2 / Biohazard Game Location";
            dialog.InitialDirectory = txtGameDataLocation.Text;
            dialog.Filter = "Executable Files (*.exe)|*.exe";
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = false;
            dialog.FileOk += (s, e2) =>
            {
                if (!NormaliseGamePath(dialog.FileName, out _))
                {
                    if (!ShowGamePathWarning())
                    {
                        e2.Cancel = true;
                    }
                }
            };
            if (dialog.ShowDialog(this) == true)
            {
                NormaliseGamePath(dialog.FileName, out var normalised);
                txtGameDataLocation.Text = normalised;
                SaveSettings();
            }
        }

        private static bool NormaliseGamePath(string path, out string normalised)
        {
            normalised = path;
            if (!Directory.Exists(normalised))
            {
                normalised = Path.GetDirectoryName(path);
            }
            return ValidateGamePath(normalised);
        }

        private static bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "data", "pl0")) ||
                Directory.Exists(Path.Combine(path, "pl0"));
        }

        private bool ShowGamePathWarning()
        {
            var title = "Incorrect RE2 location";
            var msg = "This directory was not dectected as a valid RE2 game directory. Do you want to continue anyway?";
            return MessageBox.Show(this, msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private void ShowGenerateCompleteMessage()
        {
            var title = "Randomization Complete!";
            var msg = "The Randomizer mod has successfully been generated. Run the game and choose \"BioRand: A Resident Evil Randomizer\" from the mod selection.";
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CheckForNewVersion()
        {
            Task.Run(async () =>
            {
                try
                {
                    var version = await GetLatestVersionAsync();
                    if (version > CurrentVersion)
                    {
                        Dispatcher.Invoke(() => versionBox.Visibility = Visibility.Visible);
                    }
                }
                catch
                {
                }
            });
        }

        private async Task<Version> GetLatestVersionAsync()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BioRand", CurrentVersion.ToString()));
            var response = await client.GetAsync("https://api.github.com/repos/IntelOrca/biorand/releases/latest");
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var body = JsonSerializer.Deserialize<VersionCheckBody>(jsonResponse);
                var tagName = body.tag_name;
                return Version.Parse(tagName.Substring(1));
            }
            throw new Exception("Unable to get latest version");
        }

        private void UpdateLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/IntelOrca/biorand/releases");
        }

        private void ReportIssue_Click(object sender, RoutedEventArgs e)
        {
            var body = UrlEncoder.Default.Encode("Seed: " + _config.ToString());
            Process.Start($"https://github.com/IntelOrca/biorand/issues/new?body={body}");
        }

        private void ViewDocs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start($"https://github.com/IntelOrca/biorand#readme");
        }
    }

    public class VersionCheckBody
    {
        public string tag_name { get; set; }
    }
}
