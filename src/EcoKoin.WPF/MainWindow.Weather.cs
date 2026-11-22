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

        private const string WeatherApiKeyVariable = "ECOKOIN_WEATHER_API_KEY";

        // `setx` only reaches processes started afterwards, and a terminal opened inside an already running app
        // (IDE, Claude desktop) inherits that app's old environment. So when this process has no key, read the
        // persisted per-user / per-machine value that setx stored.
        private static string ReadWeatherApiKey()
        {
            foreach (var target in new[]
                     {
                         EnvironmentVariableTarget.Process,
                         EnvironmentVariableTarget.User,
                         EnvironmentVariableTarget.Machine
                     })
            {
                string? value = Environment.GetEnvironmentVariable(WeatherApiKeyVariable, target)?.Trim();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return "";
        }

        // The service wraps every failure in WeatherUnavailableException; the inner exception says what really went wrong.
        private static string DescribeWeatherFailure(WeatherUnavailableException ex, string? city)
        {
            Exception? inner = ex.InnerException;
            return inner switch
            {
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized }
                    => "API key OpenWeatherMap ditolak (401). Cek key-nya, atau tunggu sampai key baru aktif.",
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound }
                    => $"Kota \"{city}\" tidak ditemukan. Ubah lokasi cuaca di Pengaturan.",
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                    => "Batas permintaan OpenWeatherMap tercapai. Coba lagi beberapa menit lagi.",
                HttpRequestException { StatusCode: { } code }
                    => $"OpenWeatherMap membalas kesalahan {(int)code}.",
                HttpRequestException http
                    => "Tidak bisa terhubung ke OpenWeatherMap (" + (http.InnerException?.Message ?? http.Message) + ").",
                TaskCanceledException or TimeoutException
                    => "Koneksi ke OpenWeatherMap terlalu lama (timeout 10 detik).",
                System.Text.Json.JsonException or KeyNotFoundException or FormatException or InvalidOperationException
                    => "Balasan OpenWeatherMap tidak dikenali.",
                _ => "Periksa koneksi atau lokasi cuaca di Pengaturan."
            };
        }

        // Appends the full failure to %LOCALAPPDATA%\EcoKoin\weather-error.log (the API key is redacted).
        private static void LogWeatherFailure(Exception ex)
        {
            try
            {
                string text = System.Text.RegularExpressions.Regex.Replace(
                    ex.ToString(), @"appid=[^&\s""]+", "appid=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EcoKoin");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "weather-error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception logEx) when (logEx is System.IO.IOException or UnauthorizedAccessException)
            {
                // Logging is best effort; never let it break the dashboard.
            }
        }

        private void InitializeDashboardWeather(
            DashboardViewModel dashboard, HttpClient httpClient, IClock clock, Func<WelcomePreferences> loadPreferences)
        {
            var timer = new DispatcherTimer { Interval = WeatherRefreshInterval };
            string apiKey = ReadWeatherApiKey();
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
                catch (WeatherUnavailableException ex)
                {
                    LogWeatherFailure(ex);
                    if (!request.IsCancellationRequested)
                        dashboard.ShowWeatherStatus("Cuaca tidak tersedia", DescribeWeatherFailure(ex, currentCity));
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
