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
using IntelOrca.Biohazard.RE2;

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
            SelectedGame = _settings.LastSelectedGame;
        }

        private void LoadSettings()
        {
            _settings = RandoAppSettings.Load();
            _config = _settings.Seed1 == null ? new RandoConfig() : RandoConfig.FromString(_settings.Seed1);
            if (_settings.Seed1 == null)
            {
                RandomizeSeed();
            }

            try
            {
                _suspendEvents = true;
                gameLocation1.Location = _settings.GamePath1;
                gameLocation2.Location = _settings.GamePath2;
                gameLocation3.Location = _settings.GamePath3;

                gameLocation1.IsChecked = _settings.GameEnabled1;
                gameLocation2.IsChecked = _settings.GameEnabled2;
                gameLocation3.IsChecked = _settings.GameEnabled3;
            }
            finally
            {
                _suspendEvents = false;
            }
        }

        private void SaveSettings()
        {
            _settings.GamePath1 = gameLocation1.Location;
            _settings.GamePath2 = gameLocation2.Location;
            _settings.GamePath3 = gameLocation3.Location;

            _settings.GameEnabled1 = gameLocation1.IsChecked == true;
            _settings.GameEnabled2 = gameLocation2.IsChecked == true;
            _settings.GameEnabled3 = gameLocation3.IsChecked == true;

            if (SelectedGame == 0)
                _settings.Seed1 = _config.ToString();
            else
                _settings.Seed2 = _config.ToString();
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

                chkPlayer.IsChecked = _config.ChangePlayer;
                dropdownPlayer0.SelectedIndex = _config.Player0;
                dropdownPlayer1.SelectedIndex = _config.Player1;

                chkRngChars.IsChecked = _config.RandomNPCs;
                chkNPCsRE1.IsChecked = _config.IncludeNPCRE1;
                chkNPCsRE2.IsChecked = _config.IncludeNPCRE2;
                chkNPCsOther.IsChecked = _config.IncludeNPCOther;

                chkRngBgm.IsChecked = _config.RandomBgm;
                chkBGMRE1.IsChecked = _config.IncludeBGMRE1;
                chkBGMRE2.IsChecked = _config.IncludeBGMRE2;
                chkBGMOther.IsChecked = _config.IncludeBGMOther;

                chkRngDoors.IsChecked = _config.RandomDoors;
                chkProtectSoftLock.IsChecked = _config.ProtectFromSoftLock || _config.RandomDoors;
                chkRngEnemies.IsChecked = _config.RandomEnemies;
                chkRngItems.IsChecked = _config.RandomItems || _config.RandomDoors;
                chkShuffleItems.IsChecked = _config.ShuffleItems && !_config.RandomDoors;
                chkAlternativeRoute.IsChecked = _config.AlternativeRoutes && !_config.RandomDoors;
                chkIncludeDocuments.IsChecked = _config.IncludeDocuments;
                chkRandomInventory.IsChecked = _config.RandomInventory;

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
            if (SelectedGame == 0)
                UpdateEnemyPie1();
            else
                UpdateEnemyPie2();
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

        private void UpdateEnemyPie1()
        {
            pieEnemies.Records.Clear();

            switch (_config.EnemyDifficulty)
            {
                case 0:
                    AddRatio("Crow", Colors.Black, 20);
                    AddRatio("Snake", Colors.DarkOliveGreen, 20);
                    AddRatio("Bee", Colors.Yellow, 10);
                    AddRatio("Spider", Colors.YellowGreen, 5);
                    AddRatio("Chimera", Colors.Gray, 5);
                    AddRatio("Tyrant / Yawn", Colors.DarkGray, 4);
                    AddRatio("Zombie", Colors.LightGray, 20);
                    AddRatio("Hunter", Colors.IndianRed, 5);
                    AddRatio("Cerebrus", Colors.Black, 10);
                    break;
                case 1:
                    AddRatio("Crow", Colors.Black, 10);
                    AddRatio("Snake", Colors.DarkOliveGreen, 10);
                    AddRatio("Bee", Colors.Yellow, 5);
                    AddRatio("Spider", Colors.YellowGreen, 10);
                    AddRatio("Chimera", Colors.Gray, 10);
                    AddRatio("Tyrant / Yawn", Colors.DarkGray, 8);
                    AddRatio("Zombie", Colors.LightGray, 25);
                    AddRatio("Hunter", Colors.IndianRed, 10);
                    AddRatio("Cerebrus", Colors.Black, 15);
                    break;
                case 2:
                    AddRatio("Crow", Colors.Black, 1);
                    AddRatio("Snake", Colors.DarkOliveGreen, 1);
                    AddRatio("Bee", Colors.Yellow, 1);
                    AddRatio("Spider", Colors.YellowGreen, 10);
                    AddRatio("Chimera", Colors.Gray, 15);
                    AddRatio("Tyrant / Yawn", Colors.DarkGray, 20);
                    AddRatio("Zombie", Colors.LightGray, 30);
                    AddRatio("Hunter", Colors.IndianRed, 15);
                    AddRatio("Cerebrus", Colors.Black, 20);
                    break;
                case 3:
                default:
                    AddRatio("Crow", Colors.Black, 1);
                    AddRatio("Snake", Colors.DarkOliveGreen, 1);
                    AddRatio("Bee", Colors.Yellow, 1);
                    AddRatio("Spider", Colors.YellowGreen, 12);
                    AddRatio("Chimera", Colors.Gray, 25);
                    AddRatio("Tyrant / Yawn", Colors.DarkGray, 40);
                    AddRatio("Zombie", Colors.LightGray, 20);
                    AddRatio("Hunter", Colors.IndianRed, 25);
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

        private void UpdateEnemyPie2()
        {
            pieEnemies.Records.Clear();

            switch (_config.EnemyDifficulty)
            {
                case 0:
                    AddRatio("Crow", Colors.Black, 10);
                    AddRatio("Arms", Colors.LightGray, 10);
                    AddRatio("Spider", Colors.YellowGreen, 10);
                    AddRatio("Moth", Colors.DarkOliveGreen, 2);
                    AddRatio("Ivy", Colors.SpringGreen, 3);
                    AddRatio("Ivy", Colors.Purple, 2);
                    AddRatio("Tyrant", Colors.DarkGray, 1);
                    AddRatio("Zombie", Colors.LightGray, 50);
                    AddRatio("Licker", Colors.IndianRed, 2);
                    AddRatio("Licker", Colors.Gray, 2);
                    AddRatio("Cerebrus", Colors.Black, 8);
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
            _config.ChangePlayer = chkPlayer.IsChecked == true;
            _config.Player0 = (byte)dropdownPlayer0.SelectedIndex;
            _config.Player1 = (byte)dropdownPlayer1.SelectedIndex;

            _config.RandomNPCs = chkRngChars.IsChecked == true;
            _config.IncludeNPCRE1 = chkNPCsRE1.IsChecked == true;
            _config.IncludeNPCRE2 = chkNPCsRE2.IsChecked == true;
            _config.IncludeNPCOther = chkNPCsOther.IsChecked == true;

            _config.RandomBgm = chkRngBgm.IsChecked == true;
            _config.IncludeBGMRE1 = chkBGMRE1.IsChecked == true;
            _config.IncludeBGMRE2 = chkBGMRE2.IsChecked == true;
            _config.IncludeBGMOther = chkBGMOther.IsChecked == true;

            _config.RandomDoors = chkRngDoors.IsChecked == true;
            _config.ProtectFromSoftLock = chkProtectSoftLock.IsChecked == true || _config.RandomDoors;
            _config.RandomEnemies = chkRngEnemies.IsChecked == true;
            _config.RandomItems = chkRngItems.IsChecked == true || _config.RandomDoors;
            _config.ShuffleItems = chkShuffleItems.IsChecked == true && !_config.RandomDoors;
            _config.AlternativeRoutes = chkAlternativeRoute.IsChecked == true && !_config.RandomDoors;
            _config.IncludeDocuments = chkIncludeDocuments.IsChecked == true;
            _config.RandomInventory = chkRandomInventory.IsChecked == true;

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
            _config.Player0 = (byte)_random.Next(0, dropdownPlayer0.Items.Count);
            _config.Player1 = (byte)_random.Next(0, dropdownPlayer1.Items.Count);
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

            if (_config.Game != (SelectedGame + 1))
            {
                if (_config.Game == 1 || _config.Game == 2)
                    SelectedGame = _config.Game - 1;
                else
                    _config.Game = (byte)(SelectedGame + 1);
            }
            if (_config.Game == 1)
            {
                _config.Scenario = 0;
            }

            UpdateUi();
            txtSeed.Text = _config.ToString();
            txtSeed.CaretIndex = Math.Min(caretIndex, txtSeed.Text.Length);
        }

        private ReInstallConfig GetInstallConfig()
        {
            var config = new ReInstallConfig();
            config.SetEnabled(0, _settings.GameEnabled1);
            config.SetEnabled(1, _settings.GameEnabled2);
            config.SetEnabled(2, _settings.GameEnabled3);
            config.SetInstallPath(0, _settings.GamePath1);
            config.SetInstallPath(1, _settings.GamePath2);
            config.SetInstallPath(2, _settings.GamePath3);
            return config;
        }

        private bool ValidateGameData(BaseRandomiser randomizer, string gamePath, string game)
        {
            if (!Path.IsPathRooted(gamePath) || !Directory.Exists(gamePath))
            {
                var msg = $"You have not specified an {game} game directory that exists.";
                ShowGenerateFailedMessage(msg);
                return false;
            }

            if (!randomizer.ValidateGamePath(gamePath))
            {
                if (!ShowGamePathWarning(gamePath))
                {
                    return false;
                }
            }

            try
            {
                var err = randomizer.DoIntegrityCheck(gamePath);
                if (err == 2)
                {
                    ShowFailedMessage("Integrity Check Failed", "One or more of your room files are missing.\n" +
                        $"Check that your {game} installation is integral.");
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
            return true;
        }

        private bool ValidateGamePaths()
        {
            if (_settings.GameEnabled1)
            {
                var r = GetRandomizer(0);
                if (!ValidateGameData(r, _settings.GamePath1, "RE1"))
                {
                    return false;
                }
            }
            if (_settings.GameEnabled2)
            {
                var r = GetRandomizer(1);
                if (!ValidateGameData(r, _settings.GamePath2, "RE2"))
                {
                    return false;
                }
            }
            return true;
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (!ValidateGamePaths())
            {
                return;
            }

            var btn = (Button)sender;
            try
            {
                btn.Content = "Generating...";
                IsEnabled = false;
                var randomiser = GetRandomizer();
                await Task.Run(() => randomiser.Generate(_config, GetInstallConfig()));
                RandoAppSettings.LogGeneration(_config.ToString(), GetGameLocation());
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
            if (ex is BioRandUserException)
                ShowGenerateFailedMessage(ex.Message);
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

        private bool ShowGamePathWarning(string game)
        {
            var title = $"Incorrect {game} location";
            var msg = $"This directory was not dectected as a valid {game} game directory. Do you want to continue anyway?";
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

        private void gameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = gameListView.SelectedIndex;
            if (index == 0)
                _config = RandoConfig.FromString(_settings.Seed1);
            else if (index == 1)
                _config = RandoConfig.FromString(_settings.Seed2);

            try
            {
                _suspendEvents = true;
                if (index == 3)
                {
                    panelInfo.Visibility = Visibility.Visible;
                    panelConfig.Visibility = Visibility.Hidden;
                    panelRando.Visibility = Visibility.Hidden;
                }
                else if (index == 2)
                {
                    panelInfo.Visibility = Visibility.Hidden;
                    panelConfig.Visibility = Visibility.Visible;
                    panelRando.Visibility = Visibility.Hidden;
                }
                else
                {
                    panelInfo.Visibility = Visibility.Hidden;
                    panelConfig.Visibility = Visibility.Hidden;
                    panelRando.Visibility = Visibility.Visible;
                    if (index == 0)
                    {
                        chkNPCsRE1.IsEnabled = true;
                        chkNPCsRE2.IsEnabled = false;
                        chkNPCsOther.IsEnabled = false;

                        _config.IncludeNPCRE1 = true;
                        _config.IncludeNPCRE2 = false;
                        _config.IncludeNPCRE3 = false;
                        _config.IncludeNPCOther = false;
                    }
                    else
                    {
                        chkNPCsRE1.IsEnabled = true;
                        chkNPCsRE2.IsEnabled = true;
                        chkNPCsOther.IsEnabled = true;
                    }
                    dropdownVariant.Visibility = index == 1 ?
                        Visibility.Visible :
                        Visibility.Hidden;
                    UpdatePlayerDropdowns();
                    UpdateLogButtons();
                }
            }
            finally
            {
                _suspendEvents = false;
            }

            if (index >= 0 && index <= 1)
                _config.Game = (byte)(index + 1);
            _settings.LastSelectedGame = index;
            _settings.Save();

            UpdateUi();
        }

        private BaseRandomiser GetRandomizer()
        {
            return GetRandomizer(SelectedGame.Value);
        }

        private BaseRandomiser GetRandomizer(int index)
        {
            switch (index)
            {
                case 0:
                    return new Re1Randomiser(new BiorandBgCreator());
                case 1:
                    return new Re2Randomiser(new BiorandBgCreator());
                default:
                    throw new NotImplementedException();
            }
        }

        private string GetGameLocation()
        {
            switch (SelectedGame)
            {
                case 0:
                    return _settings.GamePath1;
                case 1:
                    return _settings.GamePath2;
                case 2:
                    return _settings.GamePath3;
                default:
                    return null;
            }
        }

        private void gameLocation_Validate(object sender, PathValidateEventArgs e)
        {
            if (!Directory.Exists(e.Path))
            {
                e.Message = "Directory does not exist!";
                e.IsValid = false;
                return;
            }

            var index = int.Parse((string)((GameLocationBox)sender).Tag);
            var randomizer = GetRandomizer(index);
            if (!randomizer.ValidateGamePath(e.Path))
            {
                e.Message = "Directory not valid for this game!";
                e.IsValid = false;
                return;
            }

            var integrityError = randomizer.DoIntegrityCheck(e.Path);
            if (integrityError == 2)
            {
                e.Message = "One or more of your room files are missing.";
                e.IsValid = false;
                return;
            }
            else if (integrityError == 1)
            {
                e.Message = "One or more of your room files did not match the original version.";
                e.IsValid = false;
                return;
            }

            e.IsValid = true;
            e.Message = "Directory looks good!";
        }

        private void gameLocation_Changed(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private int? SelectedGame
        {
            get
            {
                var index = gameListView.SelectedIndex;
                if (index > 1)
                    return null;
                return index;
            }
            set
            {
                gameListView.SelectedIndex = value ?? 2;
            }
        }

        private void btnLog0_Click(object sender, RoutedEventArgs e)
        {
            ViewLog(0);
        }

        private void btnLog1_Click(object sender, RoutedEventArgs e)
        {
            ViewLog(1);
        }

        private void UpdatePlayerDropdowns()
        {
            if (SelectedGame != 1)
            {
                chkPlayer.Visibility = Visibility.Collapsed;
                return;
            }

            var randomizer = GetRandomizer();
            chkPlayer.Visibility = Visibility.Visible;

            var dropdownLabels = new[] { lblPlayer0, lblPlayer1 };
            var dropdowns = new[] { dropdownPlayer0, dropdownPlayer1 };
            for (int i = 0; i < 2; i++)
            {
                var label = dropdownLabels[i];
                var dropdown = dropdowns[i];
                label.Text = $"{randomizer.GetPlayerName(i)} becomes:";
                dropdown.ItemsSource = randomizer.GetPlayerCharacters(i);
                if (dropdown.SelectedIndex == -1)
                    dropdown.SelectedIndex = 0;
            }
        }

        private void UpdateLogButtons()
        {
            var randomizer = GetRandomizer();
            var buttons = new[] { btnLog0, btnLog1 };
            for (int i = 0; i < 2; i++)
            {
                var btn = buttons[i];
                var path = GetLogPath(i);
                btn.IsEnabled = File.Exists(path);
                btn.Content = randomizer.GetPlayerName(i) + " Log...";
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
            var location = GetGameLocation();
            if (location == null)
                return null;

            var path = Path.Combine(location, "mod_biorand", $"log_pl{player}.txt");
            return path;
        }
    }

    public class VersionCheckBody
    {
        public string tag_name { get; set; }
    }

    public class GameMenuItem
    {
        public ImageSource Image { get; set; }
    }
}
