using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

public class PowerSessionRecorderTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 11, 20, 9, 0, 0);

    private readonly TempDb _db = new();
    private readonly FakeClock _clock = new(Start);
    private readonly PowerSessionRecorder _recorder;

    public PowerSessionRecorderTests()
    {
        _db.Repository.SaveUser(new User("u1", "hermione", "1.salt.hash", 0, new DateTime(2026, 1, 2)));
        _recorder = new PowerSessionRecorder(_db.Repository, _clock);
    }

    public void Dispose() => _db.Dispose();

    private IReadOnlyList<PowerSession> Sessions(string userId = "u1")
        => _db.Repository.GetPowerSessions(userId, Start.Date, Start.Date.AddDays(2));

    [Fact]
    public void FirstTick_CreatesClosedHeartbeatSessionWithEstimatedWatts()
    {
        Assert.True(_recorder.RecordPowerSaverTick("u1", true));

        var session = Assert.Single(Sessions());
        Assert.Equal("scheme-power-saver", session.SchemeId);
        Assert.Equal(Start, session.StartTime);
        Assert.Equal(Start, session.EndTime);
        Assert.Equal(65 * 50 / 100.0, session.WattSaved, 6);
    }

    [Fact]
    public void SecondTickWithin150Seconds_ExtendsTheSameSession()
    {
        _recorder.RecordPowerSaverTick("u1", true);
        var firstId = Sessions().Single().SessionId;

        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(_recorder.RecordPowerSaverTick("u1", true));
        _clock.Advance(TimeSpan.FromSeconds(150)); // exactly at the limit still extends
        Assert.True(_recorder.RecordPowerSaverTick("u1", true));

        var session = Assert.Single(Sessions());
        Assert.Equal(firstId, session.SessionId);
        Assert.Equal(Start, session.StartTime);
        Assert.Equal(Start.AddSeconds(210), session.EndTime);
    }

    [Fact]
    public void TickAfterGapOver150Seconds_StartsNewSession()
    {
        _recorder.RecordPowerSaverTick("u1", true);
        _clock.Advance(TimeSpan.FromSeconds(151));

        Assert.True(_recorder.RecordPowerSaverTick("u1", true));

        var sessions = Sessions();
        Assert.Equal(2, sessions.Count);
        Assert.NotEqual(sessions[0].SessionId, sessions[1].SessionId);
        Assert.Equal(Start, sessions[0].EndTime);
        Assert.Equal(Start.AddSeconds(151), sessions[1].StartTime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void InactiveTick_WritesNothingAndEndsTheRun(bool? active)
    {
        Assert.False(_recorder.RecordPowerSaverTick("u1", active)); // nothing open yet: no row

        _recorder.RecordPowerSaverTick("u1", true);
        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.False(_recorder.RecordPowerSaverTick("u1", active));
        _clock.Advance(TimeSpan.FromSeconds(60)); // within 150 s of the last active tick, but the run was ended
        Assert.True(_recorder.RecordPowerSaverTick("u1", true));

        var sessions = Sessions();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(Start, sessions[0].EndTime);
        Assert.Equal(Start.AddSeconds(120), sessions[1].StartTime);
    }

    [Fact]
    public void UsersAreTrackedIndependently()
    {
        _db.Repository.SaveUser(new User("u2", "ron", "1.salt.hash", 0, new DateTime(2026, 1, 2)));

        _recorder.RecordPowerSaverTick("u1", true);
        _clock.Advance(TimeSpan.FromSeconds(60));
        _recorder.RecordPowerSaverTick("u2", true);
        _recorder.RecordPowerSaverTick("u1", true);
        _recorder.RecordPowerSaverTick("u2", false);

        Assert.Single(Sessions("u1"));
        Assert.Equal(Start.AddSeconds(60), Sessions("u1").Single().EndTime);
        Assert.Single(Sessions("u2"));
    }

    [Fact]
    public void BaselineWattOption_ScalesWattSaved()
    {
        var recorder = new PowerSessionRecorder(_db.Repository, _clock, new PowerOptions { BaselineWatt = 100 });

        recorder.RecordPowerSaverTick("u1", true);

        Assert.Equal(50, Sessions().Single().WattSaved, 6);
    }

    [Fact]
    public void RecordSleep_ShorterThanOneMinute_OrInverted_ReturnsFalse()
    {
        Assert.False(_recorder.RecordSleep("u1", Start, Start.AddSeconds(59)));
        Assert.False(_recorder.RecordSleep("u1", Start, Start));
        Assert.False(_recorder.RecordSleep("u1", Start.AddHours(1), Start));

        Assert.Empty(Sessions());
    }

    [Fact]
    public void RecordSleep_OneMinuteOrMore_SavesClosedBalancedSession()
    {
        Assert.True(_recorder.RecordSleep("u1", Start, Start.AddMinutes(1)));
        Assert.True(_recorder.RecordSleep("u1", Start.AddHours(1), Start.AddHours(3)));

        var sessions = Sessions();
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
    public void RecordedSessions_FeedCarbonFinancialReportConsistently()
    {
        // 3 power-saver ticks one minute apart (2 minutes) and a 2 hour sleep.
        _recorder.RecordPowerSaverTick("u1", true);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _recorder.RecordPowerSaverTick("u1", true);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _recorder.RecordPowerSaverTick("u1", true);
        _recorder.RecordSleep("u1", Start.AddHours(2), Start.AddHours(4));

        var report = new CarbonFinancialReport(Sessions(), DateOnly.FromDateTime(Start));

        double expected = (65 * 0.5 * (2.0 / 60)) / 1000.0 + (65 * 0.9 * 2.0) / 1000.0;
        Assert.Equal(expected, report.TotalKwhSaved, 9);
        Assert.Equal(expected, report.GenerateWeeklyChart().Last().KwhSaved, 9);
    }

    [Fact]
    public void ConcurrentTicks_DoNotThrowAndKeepOneSession()
    {
        Parallel.For(0, 20, _ => _recorder.RecordPowerSaverTick("u1", true));

        Assert.Single(Sessions());
    }
}
