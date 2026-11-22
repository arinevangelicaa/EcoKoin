using System.Net.Http;
using System.Threading;
using System.Windows.Threading;
using EcoKoin.Services;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        private static readonly HttpClient WeatherHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly TimeSpan WeatherRefreshInterval = TimeSpan.FromMinutes(10);

        private void InitializeDashboardWeather(
            DashboardViewModel dashboard, HttpClient httpClient, IClock clock, Func<WelcomePreferences> loadPreferences)
        {
            var timer = new DispatcherTimer { Interval = WeatherRefreshInterval };
            string apiKey = Environment.GetEnvironmentVariable("ECOKOIN_WEATHER_API_KEY")?.Trim() ?? "";
            string? currentCity = null;
            WeatherService? service = null;
            CancellationTokenSource? pendingRequest = null;

            async Task RefreshAsync()
            {
                var cityService = service;
                if (cityService is null || pendingRequest is not null)
                    return;

                using var request = new CancellationTokenSource();
                pendingRequest = request;
                try
                {
                    var weather = await cityService.FetchCurrentWeatherAsync(request.Token);
                    if (!request.IsCancellationRequested)
                    {
                        bool isStale = clock.Now - weather.Timestamp >= TimeSpan.FromMinutes(cityService.CacheMinutes);
                        dashboard.ShowWeather(weather, cityService.GetBrightnessRecommendation(weather), isStale);
                    }
                }
                catch (WeatherUnavailableException)
                {
                    if (!request.IsCancellationRequested)
                        dashboard.ShowWeatherStatus("Cuaca tidak tersedia", "Periksa koneksi atau lokasi cuaca di Pengaturan.");
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested)
                {
                    // A newer location was selected, or the window was closed.
                }
                finally
                {
                    if (ReferenceEquals(pendingRequest, request))
                        pendingRequest = null;
                }
            }

            async void ApplyWeatherPreferences()
            {
                var preferences = loadPreferences();
                dashboard.WeatherRecommendationsEnabled = preferences.WeatherRecommendationsEnabled;
                string city = preferences.GetWeatherLocation();
                if (string.Equals(city, currentCity, StringComparison.OrdinalIgnoreCase))
                    return;

                currentCity = city;
                DashboardLocationBadge.UpdateLocation(city);
                pendingRequest?.Cancel();
                pendingRequest = null;

                if (string.IsNullOrEmpty(apiKey))
                {
                    dashboard.ShowWeatherStatus("Cuaca tidak tersedia", "API key OpenWeatherMap belum dikonfigurasi.");
                    return;
                }

                // One service per selected city keeps responses and cached data from different cities separate.
                service = new WeatherService(httpClient, new WeatherOptions
                {
                    City = city,
                    ApiKey = apiKey,
                    CacheMinutes = (int)WeatherRefreshInterval.TotalMinutes
                }, clock);
                dashboard.ShowWeatherStatus("Memuat cuaca…", "Mengambil data OpenWeatherMap.");
                await RefreshAsync();
            }

            timer.Tick += async (_, _) => await RefreshAsync();
            Loaded += (_, _) =>
            {
                if (AuthBridge.IsAuthenticated)
                {
                    ApplyWeatherPreferences();
                    if (service is not null)
                        timer.Start();
                }
            };
            WelcomePreferencesStore.PreferencesChanged += ApplyWeatherPreferences;
            Closed += (_, _) =>
            {
                timer.Stop();
                pendingRequest?.Cancel();
                WelcomePreferencesStore.PreferencesChanged -= ApplyWeatherPreferences;
            };
        }
    }
}
