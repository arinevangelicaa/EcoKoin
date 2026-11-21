using System.Windows;
using System.Windows.Controls;
using EcoKoin.WPF.Models;

namespace EcoKoin.WPF.Components
{
    public partial class LocationBadge : System.Windows.Controls.UserControl
    {
        public LocationBadge()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WelcomePreferencesStore.PreferencesChanged += RefreshLocation;
            RefreshLocation();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WelcomePreferencesStore.PreferencesChanged -= RefreshLocation;
        }

        private void RefreshLocation()
        {
            LocationText.Text =
                WelcomePreferencesStore.Load().WeatherLocation;
        }
    }
}