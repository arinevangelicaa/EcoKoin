using System;

namespace EcoKoin.Models
{
    public class WeatherData
    {
        public double Temperature { get; set; }
        public string Condition { get; set; }
        public int Cloudiness { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
