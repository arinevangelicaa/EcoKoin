using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Demo frontend: values stay on this view for the current app session.
            // No Windows power policy, screen brightness or backend is changed.
            SaveStatusText.Text = "Preferensi tersimpan untuk sesi ini (demo frontend).";
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
            SaveStatusText.Text = "Pilihan default dipulihkan (belum disimpan permanen).";
        }
    }
}