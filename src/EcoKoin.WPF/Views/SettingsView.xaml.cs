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
            PowerProfileBox.SelectedIndex = saved.PowerSaverEnabled ? 1 : 0;
            SelectComboValue(SleepTimeBox, saved.SleepDelay);
            SelectComboValue(WeatherLocationBox, saved.WeatherLocation, createIfMissing: true);

            WeatherToggle.IsChecked = saved.WeatherRecommendationsEnabled;
            PetDesktopToggle.IsChecked = saved.PetDesktopEnabled;
            PetReminderToggle.IsChecked = saved.PetReminderEnabled;
            MissionReminderToggle.IsChecked = saved.MissionReminderEnabled;
            ClaimReminderToggle.IsChecked = saved.ClaimReminderEnabled;
            SaveStatusText.Text = "Ubah preferensi, lalu klik Simpan perubahan.";
        }

        private static void SelectComboValue(ComboBox combo, string value, bool createIfMissing = false)
        {
            var selected = combo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.Ordinal));
            if (selected == null && createIfMissing)
            {
                selected = new ComboBoxItem { Content = value };
                combo.Items.Add(selected);
            }
            if (selected != null) combo.SelectedItem = selected;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saved = WelcomePreferencesStore.Load();
            saved.PowerSaverEnabled = PowerProfileBox.SelectedIndex == 1;
            saved.SleepDelay = (SleepTimeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "5 menit";
            saved.WeatherLocation = (WeatherLocationBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Catur Tunggal";
            saved.WeatherRecommendationsEnabled = WeatherToggle.IsChecked == true;
            saved.PetDesktopEnabled = PetDesktopToggle.IsChecked == true;
            saved.PetReminderEnabled = PetReminderToggle.IsChecked == true;
            saved.MissionReminderEnabled = MissionReminderToggle.IsChecked == true;
            saved.ClaimReminderEnabled = ClaimReminderToggle.IsChecked == true;

            try
            {
                WelcomePreferencesStore.Save(saved);
                SaveStatusText.Text = "Pilihan disimpan di perangkat ini. Fitur sistem dan notifikasi belum terhubung.";
            }
            catch (Exception)
            {
                SaveStatusText.Text = "Gagal menyimpan. Periksa izin penyimpanan lalu coba lagi.";
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            PowerProfileBox.SelectedIndex = 1;
            SelectComboValue(SleepTimeBox, "5 menit");
            SelectComboValue(WeatherLocationBox, "Catur Tunggal", createIfMissing: true);
            WeatherToggle.IsChecked = true;
            PetDesktopToggle.IsChecked = true;
            PetReminderToggle.IsChecked = true;
            MissionReminderToggle.IsChecked = true;
            ClaimReminderToggle.IsChecked = true;
            SaveStatusText.Text = "Default dipulihkan di form. Klik Simpan perubahan untuk menerapkan.";
        }
    }
}