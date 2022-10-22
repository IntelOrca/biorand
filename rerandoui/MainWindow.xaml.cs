using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using rer;

namespace rerandoui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Random _random = new Random();
        private RandoConfig _config = new RandoConfig();
        private bool _suspendEvents;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEvents();
            UpdateUi();
            UpdateEnabledUi();
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
                chkProtectSoftLock.IsChecked = _config.ProtectFromSoftLock;
                chkRngEnemies.IsChecked = _config.RandomEnemies;
                chkRngItems.IsChecked = _config.RandomItems;
                chkShuffleItems.IsChecked = _config.ShuffleItems;
                chkAlternativeRoute.IsChecked = _config.AlternativeRoutes;

                sliderEnemyDifficulty.Value = _config.EnemyDifficulty;

                sliderAmmo.Value = _config.RatioAmmo;
                sliderHealth.Value = _config.RatioHealth;
                sliderInkRibbons.Value = _config.RatioInkRibbons;
                sliderAmmoQuantity.Value = _config.AmmoQuantity;

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
            _config.ProtectFromSoftLock = chkProtectSoftLock.IsChecked == true;
            _config.RandomEnemies = chkRngEnemies.IsChecked == true;
            _config.RandomItems = chkRngItems.IsChecked == true;
            _config.ShuffleItems = chkShuffleItems.IsChecked == true;
            _config.AlternativeRoutes = chkAlternativeRoute.IsChecked == true;

            _config.EnemyDifficulty = (byte)sliderEnemyDifficulty.Value;

            _config.RatioAmmo = (byte)sliderAmmo.Value;
            _config.RatioHealth = (byte)sliderHealth.Value;
            _config.RatioInkRibbons = (byte)sliderInkRibbons.Value;
            _config.AmmoQuantity = (byte)sliderAmmoQuantity.Value;

            _config.GameVariant = (byte)dropdownVariant.SelectedIndex;
        }

        private void UpdateEnabledUi()
        {
            panelEnemySliders.IsEnabled = chkRngEnemies.IsChecked == true;
            panelItemSliders.IsEnabled = chkShuffleItems.IsChecked != true;
            chkShuffleItems.IsEnabled = chkRngItems.IsChecked == true;
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
            _config.Seed = _random.Next(0, RandoConfig.MaxSeed);
            UpdateUi();
        }

        private void RandomizeConfig_Click(object sender, RoutedEventArgs e)
        {
            _config.Seed = _random.Next(0, RandoConfig.MaxSeed);
            _config.EnemyDifficulty = (byte)_random.Next(0, 4);
            _config.AmmoQuantity = (byte)_random.Next(0, 8);
            _config.RatioAmmo = (byte)_random.Next(0, 32);
            _config.RatioHealth = (byte)_random.Next(0, 32);
            _config.RatioInkRibbons = (byte)_random.Next(0, 32);
            _config.ShuffleItems = false;
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

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Program.Generate(_config);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to Generate", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
