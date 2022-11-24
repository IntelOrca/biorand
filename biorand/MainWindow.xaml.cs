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
using System.Windows.Media;
using IntelOrca.Biohazard.RE1;
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
            UpdateLogButtons();
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

                UpdateHints();
            }
            finally
            {
                _suspendEvents = false;
            }

            UpdateEnabledUi();
        }

        private void UpdateHints()
        {
            UpdateEstimateCompletion();
            UpdateItemPie();
            UpdateEnemyPie();
        }

        private void UpdateEstimateCompletion()
        {
            var colour = Colors.Green;
            if (_config.RandomDoors)
            {
                var lower = (_config.AreaCount + 1) * 60;
                var upper = (_config.AreaSize + 1) * 45;
                var lHours = ((lower + 30) / 60);
                var uHours = ((upper + 30) / 60);
                lHours = Math.Min(lHours, uHours);
                uHours = Math.Max(lHours + 1, uHours);
                string eta;
                if (_config.AreaSize <= 0)
                {
                    eta = "15 to 30 minutes";
                }
                else if (_config.AreaSize <= 1)
                {
                    eta = "30 to 60 minutes";
                    colour = Colors.Brown;
                }
                else
                {
                    eta = $"{(int)lHours} to {(int)uHours} hours";
                    colour = Colors.Red;
                }
                lblEstimateCompletionTime.Text = $"Estimate completion time: {eta} per scenario";
                lblEstimateCompletionTime.Foreground = new SolidColorBrush(colour);
            }
            else
            {
                lblEstimateCompletionTime.Text = "";
            }
        }

        private void UpdateItemPie()
        {
            var keyItems = 1 / 8.0;
            var totalRest = _config.RatioAmmo + _config.RatioHealth + _config.RatioInkRibbons;
            if (totalRest == 0)
                totalRest = 1;

            var remaining = (1 - keyItems) / totalRest;
            var ammo = _config.RatioAmmo * remaining;
            var health = _config.RatioHealth * remaining;
            var ink = _config.RatioInkRibbons * remaining;

            pieItemRatios.Records.Clear();
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Keys",
                Value = keyItems,
                Color = Colors.LightBlue
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Ammo",
                Value = ammo,
                Color = Colors.IndianRed
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Health",
                Value = health,
                Color = Colors.SpringGreen
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Ink",
                Value = ink,
                Color = Colors.Black
            });
            pieItemRatios.Update();
        }

        private void UpdateEnemyPie()
        {
            pieEnemies.Records.Clear();

            switch (_config.EnemyDifficulty)
            {
                case 0:
                    AddRatio("Crow", Colors.Black, 10);
                    AddRatio("Arms", Colors.LightGray, 10);
                    AddRatio("Spider", Colors.YellowGreen, 10);
                    AddRatio("Moth", Colors.DarkOliveGreen, 10);
                    AddRatio("Ivy", Colors.SpringGreen, 15);
                    AddRatio("Ivy", Colors.Purple, 5);
                    AddRatio("Tyrant", Colors.DarkGray, 1);
                    AddRatio("Zombie", Colors.LightGray, 30);
                    AddRatio("Licker", Colors.IndianRed, 2);
                    AddRatio("Licker", Colors.Gray, 2);
                    AddRatio("Cerebrus", Colors.Black, 5);
                    break;
                case 1:
                    AddRatio("Crow", Colors.Black, 5);
                    AddRatio("Arms", Colors.LightGray, 5);
                    AddRatio("Spider", Colors.YellowGreen, 6);
                    AddRatio("Moth", Colors.DarkOliveGreen, 5);
                    AddRatio("Ivy", Colors.SpringGreen, 6);
                    AddRatio("Ivy", Colors.Purple, 6);
                    AddRatio("Tyrant", Colors.DarkGray, 2);
                    AddRatio("Zombie", Colors.LightGray, 40);
                    AddRatio("Licker", Colors.IndianRed, 10);
                    AddRatio("Licker", Colors.Gray, 5);
                    AddRatio("Cerebrus", Colors.Black, 10);
                    break;
                case 2:
                    AddRatio("Spider", Colors.YellowGreen, 7);
                    AddRatio("Moth", Colors.DarkOliveGreen, 3);
                    AddRatio("Ivy", Colors.SpringGreen, 6);
                    AddRatio("Ivy", Colors.Purple, 6);
                    AddRatio("Tyrant", Colors.DarkGray, 3);
                    AddRatio("Zombie", Colors.LightGray, 25);
                    AddRatio("Licker", Colors.IndianRed, 15);
                    AddRatio("Licker", Colors.Gray, 10);
                    AddRatio("Cerebrus", Colors.Black, 25);
                    break;
                case 3:
                default:
                    AddRatio("Spider", Colors.YellowGreen, 5);
                    AddRatio("Moth", Colors.DarkOliveGreen, 2);
                    AddRatio("Ivy", Colors.SpringGreen, 3);
                    AddRatio("Ivy", Colors.Purple, 3);
                    AddRatio("Tyrant", Colors.DarkGray, 5);
                    AddRatio("Zombie", Colors.LightGray, 17);
                    AddRatio("Licker", Colors.IndianRed, 5);
                    AddRatio("Licker", Colors.Gray, 20);
                    AddRatio("Cerebrus", Colors.Black, 40);
                    break;
            }
            pieEnemies.Update();

            void AddRatio(string enemyName, Color color, double value)
            {
                pieEnemies.Records.Add(new PieChart.Record()
                {
                    Name = enemyName,
                    Value = value,
                    Color = color
                });
            }
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

        private BaseRandomiser GetRandomiser()
        {
            return new Re1Randomiser();
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            var randomiser = GetRandomiser();
            var gamePath = txtGameDataLocation.Text;
            if (!Path.IsPathRooted(gamePath) || !Directory.Exists(gamePath))
            {
                var msg = "You have not specified an RE2 game directory that exists.";
                ShowGenerateFailedMessage(msg);
                return;
            }

            if (!randomiser.ValidateGamePath(gamePath))
            {
                if (!ShowGamePathWarning())
                {
                    return;
                }
            }

            try
            {
                var err = randomiser.DoIntegrityCheck(gamePath);
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
                await Task.Run(() => randomiser.Generate(_config, gamePath));
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
                UpdateLogButtons();
            }
        }

        private void ShowMessageForException(Exception ex)
        {
            var accessDeniedMessage =
                "BioRand does not have permission to write to this location.\n" +
                "If your game is installed in 'Program Files', you may need to run BioRand as administrator.";

            if (ex is AggregateException)
                ex = ex.InnerException;

            if (ex is BioRandVersionException)
                ShowGenerateFailedMessage(ex.Message + "\n" + "Click the seed button to pick a new seed.");
            else if (ex is UnauthorizedAccessException)
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
            if (Directory.Exists(txtGameDataLocation.Text))
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

        private bool NormaliseGamePath(string path, out string normalised)
        {
            normalised = path;
            if (!Directory.Exists(normalised))
            {
                normalised = Path.GetDirectoryName(path);
            }

            var randomiser = GetRandomiser();
            return randomiser.ValidateGamePath(normalised);
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
            var msg = "The Randomizer mod has successfully been generated. Run the game and choose \"BioRand: A Resident Evil Randomizer\" from the mod selection." +
                "\n\nClick the log buttons to see where items were placed.";
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

        private void btnLeonLog_Click(object sender, RoutedEventArgs e)
        {
            ViewLog(0);
        }

        private void btnClaireLog_Click(object sender, RoutedEventArgs e)
        {
            ViewLog(1);
        }

        private void UpdateLogButtons()
        {
            var buttons = new[] { btnLog0, btnLog1 };
            for (int i = 0; i < 2; i++)
            {
                var btn = buttons[i];
                var path = GetLogPath(i);
                btn.IsEnabled = File.Exists(path);
            }
        }

        private void ViewLog(int player)
        {
            var path = GetLogPath(player);
            if (File.Exists(path))
            {
                Process.Start(path);
            }
            else
            {
                ShowFailedMessage("View Log", $"Unable to find log file: '{path}'");
            }
        }

        private string GetLogPath(int player)
        {
            var path = Path.Combine(txtGameDataLocation.Text, "mod_biorand", $"log_pl{player}.txt");
            return path;
        }
    }

    public class VersionCheckBody
    {
        public string tag_name { get; set; }
    }
}
