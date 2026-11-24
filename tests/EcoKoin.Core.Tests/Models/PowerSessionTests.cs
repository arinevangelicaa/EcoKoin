using EcoKoin.Models;

namespace EcoKoin.Core.Tests.Models;

public class PowerSessionTests
{
    private static readonly DateTime Start = new(2026, 11, 19, 8, 0, 0);

    [Fact]
    public void RunningSession_HasNoEndAndZeroDuration()
    {
        var s = new PowerSession("s1", "u1", "scheme", Start);
        Assert.Null(s.EndTime);
        Assert.Equal(0, s.DurationHours);
        Assert.Equal(0, s.WattSaved);
    }

    [Fact]
    public void Close_SetsEndTimeWattAndDuration()
    {
        var s = new PowerSession("s1", "u1", "scheme", Start);
        s.Close(Start.AddMinutes(90), 32.5);

        Assert.Equal(Start.AddMinutes(90), s.EndTime);
        Assert.Equal(32.5, s.WattSaved);
        Assert.Equal(1.5, s.DurationHours, 6);
    }

    [Fact]
    public void Close_Twice_Throws()
    {
        var s = new PowerSession("s1", "u1", "scheme", Start);
        s.Close(Start.AddHours(1), 10);
        Assert.Throws<InvalidOperationException>(() => s.Close(Start.AddHours(2), 20));
        Assert.Equal(10, s.WattSaved);
    }

    [Fact]
    public void Close_BeforeStart_Throws()
    {
        var s = new PowerSession("s1", "u1", "scheme", Start);
        Assert.Throws<InvalidOperationException>(() => s.Close(Start.AddMinutes(-1), 10));
        Assert.Null(s.EndTime);
    }

    [Fact]
    public void Constructor_RehydratesClosedSession()
    {
        var s = new PowerSession("s1", "u1", "scheme", Start, Start.AddHours(2), 40);
        Assert.Equal(2, s.DurationHours, 6);
        Assert.Equal(40, s.WattSaved);
    }

    [Fact]
    public void SaveToDatabaseAsync_NullRepo_Throws()
    {
        var s = new PowerSession("s1", "u1", "scheme", Start);
        Assert.Throws<ArgumentNullException>(() => { _ = s.SaveToDatabaseAsync(null!); });
    }

    [Fact]
    public async Task SaveToDatabaseAsync_DelegatesToRepository()
    {
        using var db = new EcoKoin.Core.Tests.Support.TempDb();
        await db.Repository.SaveUserAsync(new User("u1", "hermione", "1.salt.hash", 0, Start));
        var s = new PowerSession("s1", "u1", "scheme-power-saver", Start);
        s.Close(Start.AddHours(1), 25);

        await s.SaveToDatabaseAsync(db.Repository);

        var saved = Assert.Single(await db.Repository.GetWeeklyReportDataAsync("u1", Start.AddHours(2)));
        Assert.Equal("s1", saved.SessionId);
        Assert.Equal(25, saved.WattSaved);
    }
}
