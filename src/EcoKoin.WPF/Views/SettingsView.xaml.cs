using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            var saved = WelcomePreferencesStore.Load();
            if (!saved.HasCompletedOnboarding) return;
            PowerProfileBox.SelectedIndex = saved.PowerSaverEnabled ? 1 : 0;
            foreach (ComboBoxItem item in SleepTimeBox.Items)
                if (string.Equals(item.Content?.ToString(), saved.SleepDelay, StringComparison.Ordinal))
                {
                    SleepTimeBox.SelectedItem = item;
                    break;
                }

            ComboBoxItem? selected = WeatherLocationBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Content?.ToString(), saved.WeatherLocation, StringComparison.Ordinal));
            if (selected == null)
            {
                selected = new ComboBoxItem { Content = saved.WeatherLocation };
                WeatherLocationBox.Items.Add(selected);
            }
            WeatherLocationBox.SelectedItem = selected;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saved = WelcomePreferencesStore.Load();
            saved.PowerSaverEnabled = PowerProfileBox.SelectedIndex == 1;
            saved.SleepDelay = (SleepTimeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "5 menit";
            saved.WeatherLocation = (WeatherLocationBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Catur Tunggal";
            try
            {
                WelcomePreferencesStore.Save(saved);
                SaveStatusText.Text = "Preferensi awal tersimpan di perangkat ini (demo frontend).";
            }
            catch (Exception)
            {
                SaveStatusText.Text = "Tidak dapat menyimpan preferensi di perangkat ini.";
            }
            // Power, brightness, weather API, notifications remain frontend simulations.
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            PowerProfileBox.SelectedIndex = 1;
            SleepTimeBox.SelectedIndex = 1;
            WeatherLocationBox.SelectedIndex = 0;
            WeatherToggle.IsChecked = true;
            PetDesktopToggle.IsChecked = true;
            PetReminderToggle.IsChecked = true;
            MissionReminderToggle.IsChecked = true;
            ClaimReminderToggle.IsChecked = true;
            SaveStatusText.Text = "Pilihan default dipulihkan. Klik Simpan perubahan untuk menyimpan.";
        }
    }
}