namespace EcoKoin.Models
{
    /// <summary>Transient weather snapshot (not persisted). Temperature in °C, Humidity and Cloudiness in %.</summary>
    public record WeatherData(double Temperature, int Humidity, string Condition, int Cloudiness, DateTime Timestamp);
}
