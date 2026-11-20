using System.Windows;
using EcoKoin.WPF.Components;

namespace EcoKoin.WPF
{
    // New optional partial-class file; leave the existing MainWindow.xaml.cs intact.
    public partial class MainWindow
    {
        private void ApplyBrightness_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Rekomendasi: turunkan kecerahan layar ke 40%. Penerapan otomatis belum tersedia; silakan ubah lewat pengaturan Windows.",
                "Rekomendasi Cuaca", MessageBoxButton.OK, MessageBoxImage.Information);

        private void ApplySleep_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Rekomendasi: atur sleep setelah 5 menit idle. Aplikasi belum mengubah pengaturan daya Windows.",
                "Rekomendasi Hemat Daya", MessageBoxButton.OK, MessageBoxImage.Information);

        private void DashboardClaimSleep_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Klaim +30 Koin belum tersedia hingga backend misi dihubungkan.",
                "Misi Harian", MessageBoxButton.OK, MessageBoxImage.Information);

        private void DashboardClaimWeather_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Klaim +15 Koin belum tersedia hingga backend misi dihubungkan.",
                "Misi Harian", MessageBoxButton.OK, MessageBoxImage.Information);

        private void OpenFullAnalysis_Click(object sender, RoutedEventArgs e) => ShowSection("Analisis");
        private void OpenFullQuests_Click(object sender, RoutedEventArgs e) => ShowSection("Misi & Reward");

        private void ShowSection(string section)
        {
            bool dashboard = section == "Dashboard";
            bool quest = section == "Misi & Reward";
            bool analysis = section == "Analisis";
            bool settings = section == "Pengaturan";
            DashboardContent.Visibility = dashboard ? Visibility.Visible : Visibility.Collapsed;
            QuestRewardContent.Visibility = quest ? Visibility.Visible : Visibility.Collapsed;
            AnalysisContent.Visibility = analysis ? Visibility.Visible : Visibility.Collapsed;
            SettingsContent.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
            Title = "EcoKoin - " + section;
            foreach (var item in FindChildren<SidebarMenuItem>(AppSidebar))
                item.IsActive = item.MenuText == section;
        }
    }
}