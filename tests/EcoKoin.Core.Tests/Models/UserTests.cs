using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Models;

public class UserTests
{
    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime Now { get; } = now;
    }

    private static User NewUser(int koin = 0) => new("u1", "hermione", "x", koin, new DateTime(2026, 1, 1));

    [Fact]
    public void AddEcoKoin_IncreasesBalance()
    {
        var user = NewUser(10);
        user.AddEcoKoin(5);
        Assert.Equal(15, user.GetBalance());
        Assert.Equal(15, user.TotalEcoKoin);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddEcoKoin_NonPositive_Throws(int amount)
    {
        var user = NewUser(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => user.AddEcoKoin(amount));
        Assert.Equal(10, user.TotalEcoKoin);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(499, 1, 499)]
    [InlineData(500, 2, 0)]
    [InlineData(1250, 3, 250)]
    public void LevelAndXp_FollowFiveHundredPerLevel(int koin, int level, int xp)
    {
        var user = NewUser(koin);
        Assert.Equal(level, user.Level);
        Assert.Equal(xp, user.XpInLevel);
    }

    [Fact]
    public void Register_SetsFieldsAndHashesPassword()
    {
        var now = new DateTime(2026, 11, 19, 8, 0, 0);
        var user = User.Register("ani", "secret", new FixedClock(now));

        Assert.Equal("ani", user.Username);
        Assert.Equal(now, user.JoinDate);
        Assert.Equal(0, user.TotalEcoKoin);
        Assert.False(string.IsNullOrWhiteSpace(user.UserId));
        Assert.DoesNotContain("secret", user.PasswordHash);
        Assert.Equal(3, user.PasswordHash.Split('.').Length);
        Assert.StartsWith("100000.", user.PasswordHash);
    }

    [Fact]
    public void Register_SamePasswordTwice_ProducesDifferentHashes()
    {
        var a = User.Register("a", "same");
        var b = User.Register("b", "same");
        Assert.NotEqual(a.PasswordHash, b.PasswordHash);
        Assert.NotEqual(a.UserId, b.UserId);
    }

    [Theory]
    [InlineData("", "pw")]
    [InlineData("   ", "pw")]
    [InlineData("name", "")]
    public void Register_EmptyInput_Throws(string username, string password)
    {
        Assert.Throws<ArgumentException>(() => User.Register(username, password));
    }

    [Fact]
    public void Login_CorrectCredentials_ReturnsTrue()
    {
        var user = User.Register("ani", "secret");
        Assert.True(user.Login("ani", "secret"));
    }

    [Fact]
    public void Login_WrongPasswordOrUsername_ReturnsFalse()
    {
        var user = User.Register("ani", "secret");
        Assert.False(user.Login("ani", "wrong"));
        Assert.False(user.Login("other", "secret"));
        Assert.False(user.Login("ANI", "secret"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plaintext")]
    [InlineData("100000.only")]
    [InlineData("abc.AAAA.AAAA")]
    [InlineData("100000.!!!.!!!")]
    [InlineData("0.AAAA.AAAA")]
    [InlineData("100000..")]
    public void Login_MalformedStoredHash_ReturnsFalse(string storedHash)
    {
        var user = new User("u1", "ani", storedHash, 0, DateTime.UtcNow);
        Assert.False(user.Login("ani", "secret"));
    }

    [Fact]
    public void Login_HashProducedByRegister_RoundTripsThroughConstructor()
    {
        var registered = User.Register("ani", "secret");
        var loaded = new User(registered.UserId, registered.Username, registered.PasswordHash, 0, registered.JoinDate);
        Assert.True(loaded.Login("ani", "secret"));
    }
}
