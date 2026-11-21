using System.Windows;
using System.Windows.Controls;

namespace EcoKoin.WPF.Components
{
    public partial class LocationBadge : UserControl
    {
        public LocationBadge()
        {
            InitializeComponent();
            Loaded += LocationBadge_Loaded;
        }

        private void LocationBadge_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            LocationText.Text = "Menunggu lokasi perangkat";
        }

        // Dipanggil nanti saat LocationService
        // sudah mengirimkan hasil deteksi lokasi.
        public void UpdateLocation(string? location)
        {
            LocationText.Text =
                string.IsNullOrWhiteSpace(location)
                    ? "Lokasi tidak tersedia"
                    : location;
        }
    }
}