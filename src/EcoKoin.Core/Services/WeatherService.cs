using System;
using EcoKoin.Models;

namespace EcoKoin.Services
{
    public class WeatherService
    {
        public string ApiKey { get; set; }
        public string CityName { get; set; }

        public WeatherData FetchCurrentWeather()
        {
            throw new NotImplementedException();
        }

        public string GetBrightnessRecommendation(WeatherData data)
        {
            throw new NotImplementedException();
        }
    }
}
