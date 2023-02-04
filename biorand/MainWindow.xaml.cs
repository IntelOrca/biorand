using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

            var seed = SelectedGame == 0 ? _settings.Seed1 : _settings.Seed2;
            if (seed == null)
            {
                RandomizeSeed();
            }
            else
            {
                _config = RandoConfig.FromString(seed);
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
            else if (sender is RandoSlider randoSlider)
            {
                randoSlider.Value = (byte)randoSlider.Value;
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

                if (listEnemies.ItemsSource != null)
                {
                    var index = 0;
                    foreach (SliderListItem item in listEnemies.ItemsSource)
                    {
                        item.Value = _config.EnemyRatios.Length > index ? _config.EnemyRatios[index] : 0;
                        index++;
                    }
                }

                chkRngChars.IsChecked = _config.RandomNPCs;
                if (listNPCs.ItemsSource != null)
                {
                    var index = 0;
                    foreach (CheckBoxListItem item in listNPCs.ItemsSource)
                    {
                        item.IsChecked = _config.EnabledNPCs.Length > index ? _config.EnabledNPCs[index] : false;
                        index++;
                    }
                }

                chkRngBgm.IsChecked = _config.RandomBgm;
                if (listBGMs.ItemsSource != null)
                {
                    var index = 0;
                    foreach (CheckBoxListItem item in listBGMs.ItemsSource)
                    {
                        item.IsChecked = _config.EnabledBGMs.Length > index ? _config.EnabledBGMs[index] : false;
                        index++;
                    }
                }

                chkRngDoors.IsChecked = _config.RandomDoors;
                chkProtectSoftLock.IsChecked = _config.ProtectFromSoftLock || _config.RandomDoors;
                chkRngEnemies.IsChecked = _config.RandomEnemies;
                chkRandomEnemyPlacements.IsChecked = _config.RandomEnemyPlacement;
                chkEnemyRestrictedRooms.IsChecked = _config.AllowEnemiesAnyRoom;
                sliderEnemyCount.Value = _config.EnemyQuantity;
                chkRngItems.IsChecked = _config.RandomItems || _config.RandomDoors;
                chkShuffleItems.IsChecked = _config.ShuffleItems && !_config.RandomDoors;
                chkAlternativeRoute.IsChecked = _config.AlternativeRoutes && !_config.RandomDoors;
                chkIncludeDocuments.IsChecked = _config.IncludeDocuments;
                chkRandomInventory.IsChecked = _config.RandomInventory;

                dropdownWeapon0.SelectedIndex = _config.Weapon0;
                dropdownWeapon1.SelectedIndex = _config.Weapon1;
                sliderWeaponQuantity.Value = _config.WeaponQuantity;

                sliderEnemyDifficulty.Value = _config.EnemyDifficulty;

                sliderAmmo.Value = _config.RatioAmmo;
                sliderHealth.Value = _config.RatioHealth;
                sliderInkRibbons.Value = _config.RatioInkRibbons;
                sliderAmmoQuantity.Value = _config.AmmoQuantity;

                sliderAreaCount.Value = _config.AreaCount;
                sliderAreaSize.Value = _config.AreaSize;

                dropdownVariant.SelectedIndex = _config.GameVariant;

                txtSeed.Text = _config.ToString();
                seedQrCode.Seed = null;
                seedQrCode.Seed = _config;

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

            var randomizer = GetRandomizer();
            if (randomizer != null)
            {
                var enemies = randomizer.GetEnemies();
                var index = 0;
                foreach (var enemy in enemies)
                {
                    var colour = (Color)ColorConverter.ConvertFromString(enemy.Colour);
                    var ratio = _config.EnemyRatios.Length > index ?
                        _config.EnemyRatios[index] :
                        0;
                    if (ratio != 0)
                    {
                        pieEnemies.Records.Add(new PieChart.Record()
                        {
                            Name = enemy.Name,
                            Value = ratio,
                            Color = colour
                        });
                    }
                    index++;
                }
            }

            pieEnemies.Update();
        }

        private void UpdateConfig()
        {
            _config.ChangePlayer = chkPlayer.IsChecked == true;
            _config.Player0 = (byte)dropdownPlayer0.SelectedIndex;
            _config.Player1 = (byte)dropdownPlayer1.SelectedIndex;

            _config.EnemyRatios = listEnemies.ItemsSource
                .Cast<SliderListItem>()
                .Select(x => (byte)x.Value)
                .ToArray();

            _config.RandomNPCs = chkRngChars.IsChecked == true;
            _config.EnabledNPCs = listNPCs.ItemsSource
                .Cast<CheckBoxListItem>()
                .Select(x => x.IsChecked)
                .ToArray();

            _config.RandomBgm = chkRngBgm.IsChecked == true;
            _config.EnabledBGMs = listBGMs.ItemsSource
                .Cast<CheckBoxListItem>()
                .Select(x => x.IsChecked)
                .ToArray();

            _config.RandomDoors = chkRngDoors.IsChecked == true;
            _config.ProtectFromSoftLock = chkProtectSoftLock.IsChecked == true || _config.RandomDoors;

            _config.RandomEnemies = chkRngEnemies.IsChecked == true;
            _config.AllowEnemiesAnyRoom = chkEnemyRestrictedRooms.IsChecked == true;
            _config.EnemyQuantity = (byte)sliderEnemyCount.Value;
            _config.EnemyDifficulty = (byte)sliderEnemyDifficulty.Value;

            _config.RandomItems = chkRngItems.IsChecked == true || _config.RandomDoors;
            _config.RandomEnemyPlacement = chkRandomEnemyPlacements.IsChecked == true;
            _config.ShuffleItems = chkShuffleItems.IsChecked == true && !_config.RandomDoors;
            _config.AlternativeRoutes = chkAlternativeRoute.IsChecked == true && !_config.RandomDoors;
            _config.IncludeDocuments = chkIncludeDocuments.IsChecked == true;
            _config.RandomInventory = chkRandomInventory.IsChecked == true;
            _config.Weapon0 = (byte)dropdownWeapon0.SelectedIndex;
            _config.Weapon1 = (byte)dropdownWeapon1.SelectedIndex;
            _config.WeaponQuantity = (byte)sliderWeaponQuantity.Value;

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
            dropdownWeapon0.IsEnabled = chkRandomInventory.IsChecked == true;
            dropdownWeapon1.IsEnabled = chkRandomInventory.IsChecked == true;
            panelItemSliders.IsEnabled = chkShuffleItems.IsChecked != true;
            chkShuffleItems.IsEnabled = chkRngDoors.IsChecked != true;
            chkAlternativeRoute.IsEnabled = chkRngDoors.IsChecked != true;
            chkProtectSoftLock.IsEnabled = chkRngDoors.IsChecked != true;
            chkEnemyRestrictedRooms.IsEnabled = chkRandomEnemyPlacements.IsChecked == true;
            sliderEnemyCount.IsEnabled = chkRandomEnemyPlacements.IsChecked == true;
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

            switch (ex)
            {
                case BioRandVersionException _:
                    ShowGenerateFailedMessage(ex.Message + "\n" + "Click the seed button to pick a new seed.");
                    break;
                case BioRandUserException _:
                    ShowGenerateFailedMessage(ex.Message);
                    break;
                case UnauthorizedAccessException _:
                    ShowGenerateFailedMessage(accessDeniedMessage);
                    break;
                case Exception _ when IsTypicalException(ex):
                    ShowGenerateFailedMessage("An error occured during generation.\nPlease report this seed and try another.");
                    break;
                default:
                    ShowGenerateFailedMessage(ex.Message + "\n" + "Please report this seed and try another.");
                    break;
            }
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
            var version = UrlEncoder.Default.Encode(string.Format("{0} ({1})", Program.CurrentVersionNumber, Program.GitHash));
            var seed = UrlEncoder.Default.Encode(_config.ToString());
            Process.Start($"https://github.com/IntelOrca/biorand/issues/new?template=bug_report.yml&version={version}&seed={seed}");
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
                        chkRandomEnemyPlacements.Visibility = Visibility.Hidden;
                        chkEnemyRestrictedRooms.Visibility = Visibility.Hidden;
                        sliderEnemyCount.Visibility = Visibility.Hidden;

                        _config.RandomEnemyPlacement = false;
                    }
                    else
                    {
                        chkRandomEnemyPlacements.Visibility = Visibility.Visible;
                        chkEnemyRestrictedRooms.Visibility = Visibility.Visible;
                        sliderEnemyCount.Visibility = Visibility.Visible;
                    }
                    dropdownVariant.Visibility = index == 1 ?
                        Visibility.Visible :
                        Visibility.Hidden;
                    UpdatePlayerDropdowns();
                    UpdateEnemies();
                    UpdateNPCList();
                    UpdateBGMList();
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
            if (SelectedGame == null)
                return null;
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
                    return null;
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
            if (SelectedGame == 3)
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

        private void UpdateEnemies()
        {
            var randomizer = GetRandomizer();
            var items = new List<SliderListItem>();
            foreach (var enemy in randomizer.GetEnemies())
            {
                items.Add(new SliderListItem(enemy.Name, 4, 7));
            }
            listEnemies.ItemsSource = items;
        }

        private void UpdateNPCList()
        {
            var randomizer = GetRandomizer();
            listNPCs.ItemsSource = randomizer
                .GetNPCs()
                .Select(x => new CheckBoxListItem(x.ToActorString(), true))
                .ToArray();
        }

        private void UpdateBGMList()
        {
            var randomizer = GetRandomizer();
            listBGMs.ItemsSource = randomizer
                .GetMusicAlbums()
                .Select(x => new CheckBoxListItem(x, true))
                .ToArray();
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
                var hyperlink = btn.Inlines.FirstInline as Hyperlink;
                hyperlink.Inlines.Clear();
                hyperlink.Inlines.Add($"{randomizer.GetPlayerName(i)} Log");
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

    public class CheckBoxListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _text;
        private bool _isChecked;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public CheckBoxListItem(string text, bool isChecked)
        {
            _text = text;
            _isChecked = isChecked;
        }
    }

    public class SliderListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _text;
        private double _value;
        private double _maximum;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public double Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public double Maximum
        {
            get => _maximum;
            set
            {
                if (_maximum != value)
                {
                    _maximum = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Maximum)));
                }
            }
        }

        public SliderListItem(string text, double value, int max)
        {
            Text = text;
            Value = value;
            Maximum = max;
        }
    }
}
