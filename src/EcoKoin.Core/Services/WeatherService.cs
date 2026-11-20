using System.Text.Json;
using EcoKoin.Models;

namespace EcoKoin.Services
{
    /// <summary>Spec: docs/03-domain-model.md (WeatherService). OpenWeatherMap current weather, cached CacheMinutes.</summary>
    public class WeatherService
    {
        private readonly HttpClient _http;
        private readonly WeatherOptions _options;
        private readonly IClock _clock;

        private WeatherData? _cache;
        private DateTime _cachedAt;

        public WeatherService(HttpClient http, WeatherOptions options, IClock clock)
        {
            _http = http;
            _options = options;
            _clock = clock;
        }

        /// <summary>Cache lifetime in minutes (data older than this is considered stale).</summary>
        public int CacheMinutes => _options.CacheMinutes;

        /// <summary>Synchronous wrapper over <see cref="FetchCurrentWeatherAsync"/>.</summary>
        public WeatherData FetchCurrentWeather()
        {
            return FetchCurrentWeatherAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// GET {BaseUrl}?q={City}&amp;units=metric&amp;appid={ApiKey}; maps main.temp, main.humidity, weather[0].main, clouds.all.
        /// Within CacheMinutes returns the cache. On failure returns last cache if any, else throws WeatherUnavailableException.
        /// </summary>
        public async Task<WeatherData> FetchCurrentWeatherAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new WeatherUnavailableException("Weather API key is not configured (ECOKOIN_WEATHER_API_KEY).");

            var now = _clock.Now;
            if (_cache is not null && now - _cachedAt < TimeSpan.FromMinutes(_options.CacheMinutes))
                return _cache;

            try
            {
                var url = $"{_options.BaseUrl}?q={Uri.EscapeDataString(_options.City)}&units=metric&appid={Uri.EscapeDataString(_options.ApiKey)}";
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var data = Parse(json, now);

                _cache = data;
                _cachedAt = now;
                return data;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException
                                        or InvalidOperationException or FormatException
                                        or TaskCanceledException or OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    throw;
                if (_cache is not null)
                    return _cache;
                throw new WeatherUnavailableException("Weather data is unavailable.", ex);
            }
        }

        /// <summary>Cloudiness &gt;= 60: "Turunkan kecerahan ke 40%"; 30..59: "Turunkan kecerahan ke 60%"; &lt; 30: "Kecerahan sudah optimal".</summary>
        public string GetBrightnessRecommendation(WeatherData data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Cloudiness >= 60) return "Turunkan kecerahan ke 40%";
            if (data.Cloudiness >= 30) return "Turunkan kecerahan ke 60%";
            return "Kecerahan sudah optimal";
        }

        private static WeatherData Parse(string json, DateTime timestamp)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double temp = root.GetProperty("main").GetProperty("temp").GetDouble();
            int humidity = root.GetProperty("main").GetProperty("humidity").GetInt32();
            string condition = root.GetProperty("weather")[0].GetProperty("main").GetString() ?? "";
            int clouds = root.GetProperty("clouds").GetProperty("all").GetInt32();

            return new WeatherData(temp, humidity, condition, clouds, timestamp);
        }
    }
}
