using System.Windows.Controls;

namespace EcoKoin.WPF.Components
{
    public partial class LocationBadge : UserControl
    {
        public LocationBadge()
        {
            InitializeComponent();
        }

        public void UpdateLocation(string? location)
        {
            LocationText.Text =
                string.IsNullOrWhiteSpace(location)
                    ? "Lokasi tidak tersedia"
                    : location;
        }
    }
}