using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Views
{
    public partial class AnalysisView : UserControl
    {
        public AnalysisView()
        {
            InitializeComponent();
        }

        private void PeriodComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            // SelectionChanged dapat dipanggil saat InitializeComponent.
            // Pastikan semua komponen sudah dibuat.
            if (PeriodDescription == null ||
                WeeklyKpiCards == null ||
                WeeklyTrendChart == null ||
                UnavailablePeriodPanel == null ||
                UnavailablePeriodDescription == null)
            {
                return;
            }

            switch (PeriodComboBox.SelectedIndex)
            {
                case 0:
                    // Minggu ini: data ilustrasi yang tersedia.
                    PeriodDescription.Text =
                        "7 hari terakhir · Senin–Minggu";

                    WeeklyKpiCards.Visibility = Visibility.Visible;
                    WeeklyTrendChart.Visibility = Visibility.Visible;
                    UnavailablePeriodPanel.Visibility = Visibility.Collapsed;
                    break;

                case 1:
                    // Bulan ini: data belum tersedia.
                    PeriodDescription.Text = "Periode bulanan";

                    WeeklyKpiCards.Visibility = Visibility.Hidden;
                    WeeklyTrendChart.Visibility = Visibility.Collapsed;
                    UnavailablePeriodPanel.Visibility = Visibility.Visible;

                    UnavailablePeriodDescription.Text =
                        "Data bulanan belum terhubung. " +
                        "Grafik mingguan tidak ditampilkan agar tidak menyesatkan.";
                    break;

                case 2:
                    // Tahun ini: data belum tersedia.
                    PeriodDescription.Text = "Periode tahunan";

                    WeeklyKpiCards.Visibility = Visibility.Hidden;
                    WeeklyTrendChart.Visibility = Visibility.Collapsed;
                    UnavailablePeriodPanel.Visibility = Visibility.Visible;

                    UnavailablePeriodDescription.Text =
                        "Data tahunan belum terhubung. " +
                        "Grafik mingguan tidak ditampilkan agar tidak menyesatkan.";
                    break;
            }
        }
    }
}