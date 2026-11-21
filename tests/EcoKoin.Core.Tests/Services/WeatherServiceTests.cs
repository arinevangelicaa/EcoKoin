using System.Net;
using System.Net.Http;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

public class WeatherServiceTests
{
    private const string Json = """
        {"weather":[{"id":804,"main":"Clouds","description":"overcast clouds"}],
         "main":{"temp":28.4,"humidity":78},
         "clouds":{"all":75},"name":"Yogyakarta"}
        """;

    private static readonly DateTime T0 = new(2026, 11, 20, 9, 0, 0);

    private static (WeatherService svc, StubHttpHandler handler, FakeClock clock) Create(string apiKey = "KEY", string city = "Yogyakarta")
    {
        var handler = new StubHttpHandler
        {
            Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json) }
        };
        var clock = new FakeClock(T0);
        var svc = new WeatherService(new HttpClient(handler), new WeatherOptions { ApiKey = apiKey, City = city, CacheMinutes = 10 }, clock);
        return (svc, handler, clock);
    }

    [Fact]
    public async Task Fetch_MapsOpenWeatherJson()
    {
        var (svc, handler, _) = Create();

        var data = await svc.FetchCurrentWeatherAsync();

        Assert.Equal(28.4, data.Temperature);
        Assert.Equal(78, data.Humidity);
        Assert.Equal("Clouds", data.Condition);
        Assert.Equal(75, data.Cloudiness);
        Assert.Equal(T0, data.Timestamp);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Fetch_BuildsExpectedUrl()
    {
        var (svc, handler, _) = Create(apiKey: "a b&c", city: "Kota Baru");

        await svc.FetchCurrentWeatherAsync();

        var url = handler.Requests[0].AbsoluteUri;
        Assert.StartsWith("https://api.openweathermap.org/data/2.5/weather?", url);
        Assert.Contains("q=Kota%20Baru", url);
        Assert.Contains("units=metric", url);
        Assert.Contains("appid=a%20b%26c", url);
    }

    [Fact]
    public void FetchCurrentWeather_SyncWrapper_Works()
    {
        var (svc, _, _) = Create();
        Assert.Equal(75, svc.FetchCurrentWeather().Cloudiness);
    }

    [Fact]
    public async Task Fetch_WithinCacheWindow_DoesNotCallHttpAgain()
    {
        var (svc, handler, clock) = Create();
        await svc.FetchCurrentWeatherAsync();
        clock.Advance(TimeSpan.FromMinutes(9));

        var again = await svc.FetchCurrentWeatherAsync();

        Assert.Single(handler.Requests);
        Assert.Equal(T0, again.Timestamp);
    }

    [Fact]
    public async Task Fetch_AfterCacheWindow_RefetchesAndUpdatesTimestamp()
    {
        var (svc, handler, clock) = Create();
        await svc.FetchCurrentWeatherAsync();
        clock.Advance(TimeSpan.FromMinutes(10));

        var again = await svc.FetchCurrentWeatherAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(T0.AddMinutes(10), again.Timestamp);
    }

    [Fact]
    public async Task Fetch_Http500_ReturnsStaleCache()
    {
        var (svc, handler, clock) = Create();
        var first = await svc.FetchCurrentWeatherAsync();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        clock.Advance(TimeSpan.FromMinutes(30));

        var second = await svc.FetchCurrentWeatherAsync();

        Assert.Equal(first, second);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Fetch_NetworkError_ReturnsStaleCache()
    {
        var (svc, handler, clock) = Create();
        var first = await svc.FetchCurrentWeatherAsync();
        handler.Respond = _ => throw new HttpRequestException("offline");
        clock.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(first, await svc.FetchCurrentWeatherAsync());
    }

    [Fact]
    public async Task Fetch_Http500_NoCache_Throws()
    {
        var (svc, handler, _) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<WeatherUnavailableException>(() => svc.FetchCurrentWeatherAsync());
    }

    [Fact]
    public async Task Fetch_MalformedJson_NoCache_Throws()
    {
        var (svc, handler, _) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"main\":{}}") };

        await Assert.ThrowsAsync<WeatherUnavailableException>(() => svc.FetchCurrentWeatherAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fetch_MissingApiKey_ThrowsWithoutHttpCall(string key)
    {
        var (svc, handler, _) = Create(apiKey: key);

        await Assert.ThrowsAsync<WeatherUnavailableException>(() => svc.FetchCurrentWeatherAsync());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Fetch_Cancelled_ThrowsOperationCanceled()
    {
        var (svc, handler, _) = Create();
        handler.Respond = _ => throw new TaskCanceledException();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.FetchCurrentWeatherAsync(cts.Token));
    }

    [Theory]
    [InlineData(0, "Kecerahan sudah optimal")]
    [InlineData(29, "Kecerahan sudah optimal")]
    [InlineData(30, "Turunkan kecerahan ke 60%")]
    [InlineData(59, "Turunkan kecerahan ke 60%")]
    [InlineData(60, "Turunkan kecerahan ke 40%")]
    [InlineData(100, "Turunkan kecerahan ke 40%")]
    public void GetBrightnessRecommendation_Thresholds(int clouds, string expected)
    {
        var (svc, _, _) = Create();
        var data = new WeatherData(25, 50, "Clouds", clouds, T0);

        Assert.Equal(expected, svc.GetBrightnessRecommendation(data));
    }
}
