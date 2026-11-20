using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Views
{
    public partial class QuestRewardView : UserControl
    {
        public QuestRewardView() => InitializeComponent();

        // Explicit feedback is better UX than a button that silently does nothing.
        // Do not add coins client-side until a backend confirms an authorized claim.
        private void ClaimSleep_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Misi sleep sudah selesai, tetapi klaim +30 Koin belum tersedia sampai sistem penyimpanan koin terhubung.",
                "Klaim EcoKoin", MessageBoxButton.OK, MessageBoxImage.Information);

        private void ClaimWeather_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Misi cuaca sudah selesai, tetapi klaim +15 Koin belum tersedia sampai sistem penyimpanan koin terhubung.",
                "Klaim EcoKoin", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}