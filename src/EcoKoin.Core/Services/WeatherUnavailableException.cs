namespace EcoKoin.Services
{
    /// <summary>Thrown when weather cannot be fetched and no cached value exists.</summary>
    public class WeatherUnavailableException : Exception
    {
        public WeatherUnavailableException() { }
        public WeatherUnavailableException(string message) : base(message) { }
        public WeatherUnavailableException(string message, Exception inner) : base(message, inner) { }
    }
}
