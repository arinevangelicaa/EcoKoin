using EcoKoin.Core.Tests.Support;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

public class PowerSessionRecorderTests : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 11, 20, 9, 0, 0);

    private readonly TempDb _db = new();
    private readonly FakeClock _clock = new(Start);
    private readonly PowerSessionRecorder _recorder;

    public PowerSessionRecorderTests()
    {
        _recorder = new PowerSessionRecorder(_db.Repository, _clock);
    }

    public async Task InitializeAsync()
        => await _db.Repository.SaveUserAsync(new User("u1", "hermione", "1.salt.hash", 0, new DateTime(2026, 1, 2)));

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<PowerSession>> Sessions(string userId = "u1")
        => await _db.Repository.GetPowerSessionsAsync(userId, Start.Date, Start.Date.AddDays(2));

    [Fact]
    public async Task FirstTick_CreatesClosedHeartbeatSessionWithEstimatedWatts()
    {
        Assert.True(await _recorder.RecordPowerSaverTickAsync("u1", true));

        var session = Assert.Single(await Sessions());
        Assert.Equal("scheme-power-saver", session.SchemeId);
        Assert.Equal(Start, session.StartTime);
        Assert.Equal(Start, session.EndTime);
        Assert.Equal(65 * 50 / 100.0, session.WattSaved, 6);
    }

    [Fact]
    public async Task SecondTickWithin150Seconds_ExtendsTheSameSession()
    {
        await _recorder.RecordPowerSaverTickAsync("u1", true);
        var firstId = (await Sessions()).Single().SessionId;

        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(await _recorder.RecordPowerSaverTickAsync("u1", true));
        _clock.Advance(TimeSpan.FromSeconds(150)); // exactly at the limit still extends
        Assert.True(await _recorder.RecordPowerSaverTickAsync("u1", true));

        var session = Assert.Single(await Sessions());
        Assert.Equal(firstId, session.SessionId);
        Assert.Equal(Start, session.StartTime);
        Assert.Equal(Start.AddSeconds(210), session.EndTime);
    }

    [Fact]
    public async Task TickAfterGapOver150Seconds_StartsNewSession()
    {
        await _recorder.RecordPowerSaverTickAsync("u1", true);
        _clock.Advance(TimeSpan.FromSeconds(151));

        Assert.True(await _recorder.RecordPowerSaverTickAsync("u1", true));

        var sessions = await Sessions();
        Assert.Equal(2, sessions.Count);
        Assert.NotEqual(sessions[0].SessionId, sessions[1].SessionId);
        Assert.Equal(Start, sessions[0].EndTime);
        Assert.Equal(Start.AddSeconds(151), sessions[1].StartTime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task InactiveTick_WritesNothingAndEndsTheRun(bool? active)
    {
        Assert.False(await _recorder.RecordPowerSaverTickAsync("u1", active)); // nothing open yet: no row

        await _recorder.RecordPowerSaverTickAsync("u1", true);
        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.False(await _recorder.RecordPowerSaverTickAsync("u1", active));
        _clock.Advance(TimeSpan.FromSeconds(60)); // within 150 s of the last active tick, but the run was ended
        Assert.True(await _recorder.RecordPowerSaverTickAsync("u1", true));

        var sessions = await Sessions();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(Start, sessions[0].EndTime);
        Assert.Equal(Start.AddSeconds(120), sessions[1].StartTime);
    }

    [Fact]
    public async Task UsersAreTrackedIndependently()
    {
        await _db.Repository.SaveUserAsync(new User("u2", "ron", "1.salt.hash", 0, new DateTime(2026, 1, 2)));

        await _recorder.RecordPowerSaverTickAsync("u1", true);
        _clock.Advance(TimeSpan.FromSeconds(60));
        await _recorder.RecordPowerSaverTickAsync("u2", true);
        await _recorder.RecordPowerSaverTickAsync("u1", true);
        await _recorder.RecordPowerSaverTickAsync("u2", false);

        var first = Assert.Single(await Sessions("u1"));
        Assert.Equal(Start.AddSeconds(60), first.EndTime);
        Assert.Single(await Sessions("u2"));
    }

    [Fact]
    public async Task BaselineWattOption_ScalesWattSaved()
    {
        var recorder = new PowerSessionRecorder(_db.Repository, _clock, new PowerOptions { BaselineWatt = 100 });

        await recorder.RecordPowerSaverTickAsync("u1", true);

        Assert.Equal(50, (await Sessions()).Single().WattSaved, 6);
    }

    [Fact]
    public async Task RecordSleep_ShorterThanOneMinute_OrInverted_ReturnsFalse()
    {
        Assert.False(await _recorder.RecordSleepAsync("u1", Start, Start.AddSeconds(59)));
        Assert.False(await _recorder.RecordSleepAsync("u1", Start, Start));
        Assert.False(await _recorder.RecordSleepAsync("u1", Start.AddHours(1), Start));

        Assert.Empty(await Sessions());
    }

    [Fact]
    public async Task RecordSleep_OneMinuteOrMore_SavesClosedBalancedSession()
    {
        Assert.True(await _recorder.RecordSleepAsync("u1", Start, Start.AddMinutes(1)));
        Assert.True(await _recorder.RecordSleepAsync("u1", Start.AddHours(1), Start.AddHours(3)));

        var sessions = await Sessions();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s =>
        {
            Assert.Equal("scheme-balanced", s.SchemeId);
            Assert.NotNull(s.EndTime);
            Assert.Equal(65 * 0.9, s.WattSaved, 6);
        });
        Assert.Equal(Start.AddHours(3), sessions[1].EndTime);
    }

    [Fact]
    public async Task RecordedSessions_FeedCarbonFinancialReportConsistently()
    {
        // 3 power-saver ticks one minute apart (2 minutes) and a 2 hour sleep.
        await _recorder.RecordPowerSaverTickAsync("u1", true);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _recorder.RecordPowerSaverTickAsync("u1", true);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _recorder.RecordPowerSaverTickAsync("u1", true);
        await _recorder.RecordSleepAsync("u1", Start.AddHours(2), Start.AddHours(4));

        var report = new CarbonFinancialReport(await Sessions(), DateOnly.FromDateTime(Start));

        double expected = (65 * 0.5 * (2.0 / 60)) / 1000.0 + (65 * 0.9 * 2.0) / 1000.0;
        Assert.Equal(expected, report.TotalKwhSaved, 9);
        Assert.Equal(expected, report.GenerateWeeklyChart().Last().KwhSaved, 9);
    }

    [Fact]
    public async Task ConcurrentTicks_DoNotThrowAndKeepOneSession()
    {
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => _recorder.RecordPowerSaverTickAsync("u1", true)));

        Assert.Single(await Sessions());
    }
}
