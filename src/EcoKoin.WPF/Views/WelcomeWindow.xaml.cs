using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF.Views
{
    public partial class WelcomeWindow : Window
    {
        private readonly WelcomePreferences _preferences;

        public WelcomeWindow()
        {
            InitializeComponent();
            _preferences = WelcomePreferencesStore.Load();
            PowerSaverToggle.IsChecked = _preferences.PowerSaverEnabled;

            foreach (ComboBoxItem item in SleepCombo.Items)
            {
                if (string.Equals(item.Content?.ToString(), _preferences.SleepDelay, StringComparison.Ordinal))
                {
                    SleepCombo.SelectedItem = item;
                    break;
                }
            }
            UpdateLocationButton();
        }

        private void PowerSaverToggle_Changed(object sender, RoutedEventArgs e)
        {
            // The switch updates the selected preference only; no Windows power profile is changed.
        }

        private void UpdateLocationButton() =>
            EditLocationButton.Content = _preferences.WeatherLocation + "  ✎";

        private void EditLocation_Click(object sender, RoutedEventArgs e)
        {
            LocationInput.Text = _preferences.WeatherLocation;
            EditLocationButton.Visibility = Visibility.Hidden;
            LocationEditor.Visibility = Visibility.Visible;
            LocationInput.Focus();
            LocationInput.SelectAll();
        }

        private void SaveLocation_Click(object sender, RoutedEventArgs e)
        {
            string value = LocationInput.Text.Trim();
            if (value.Length < 2)
            {
                MessageBox.Show(this, "Masukkan lokasi minimal 2 karakter.",
                    "Lokasi belum valid", MessageBoxButton.OK, MessageBoxImage.Information);
                LocationInput.Focus();
                return;
            }
            _preferences.WeatherLocation = value;
            CloseLocationEditor();
        }

        private void CancelLocation_Click(object sender, RoutedEventArgs e) => CloseLocationEditor();

        private void CloseLocationEditor()
        {
            UpdateLocationButton();
            LocationEditor.Visibility = Visibility.Collapsed;
            EditLocationButton.Visibility = Visibility.Visible;
        }

        private bool TryCommit(bool useDefaults)
        {
            if (LocationEditor.Visibility == Visibility.Visible)
            {
                MessageBox.Show(this, "Simpan atau batalkan perubahan lokasi terlebih dahulu.",
                    "Perubahan lokasi", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (!useDefaults)
            {
                _preferences.PowerSaverEnabled = PowerSaverToggle.IsChecked == true;
                _preferences.SleepDelay = (SleepCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "5 menit";
            }
            else
            {
                _preferences.PowerSaverEnabled = true;
                _preferences.SleepDelay = "5 menit";
                _preferences.WeatherLocation = "Catur Tunggal";
            }
            _preferences.HasCompletedOnboarding = true;
            try
            {
                WelcomePreferencesStore.Save(_preferences);
                DialogResult = true; // Closes the modal welcome window.
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Pengaturan belum dapat disimpan di perangkat ini. " + ex.Message,
                    "Gagal menyimpan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e) => TryCommit(false);
        private void Skip_Click(object sender, RoutedEventArgs e) => TryCommit(true);
    }
}