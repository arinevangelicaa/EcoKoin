using System.Windows.Controls;

namespace EcoKoin.WPF.Components
{
    using EcoKoin.WPF.Models;

    public partial class LocationBadge : UserControl
    {
        public LocationBadge()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (!UseSavedLocation)
                    return;

                RefreshSavedLocation();
                WelcomePreferencesStore.PreferencesChanged += RefreshSavedLocation;
            };
            Unloaded += (_, _) => WelcomePreferencesStore.PreferencesChanged -= RefreshSavedLocation;
        }

        public bool UseSavedLocation { get; set; } = true;

        private void RefreshSavedLocation() => UpdateLocation(WelcomePreferencesStore.Load().GetWeatherLocation());

        public void UpdateLocation(string? location)
        {
            LocationText.Text =
                string.IsNullOrWhiteSpace(location)
                    ? "Lokasi tidak tersedia"
                    : location;
        }
    }
}
