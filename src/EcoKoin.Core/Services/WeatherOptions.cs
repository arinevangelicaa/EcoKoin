namespace EcoKoin.Services
{
    public class WeatherOptions
    {
        public string City { get; set; } = "Yogyakarta";
        public string ApiKey { get; set; } = "";
        public int CacheMinutes { get; set; } = 10;
        public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5/weather";
    }
}
