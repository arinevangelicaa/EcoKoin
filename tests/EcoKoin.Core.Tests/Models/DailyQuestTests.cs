using EcoKoin.Models;

namespace EcoKoin.Core.Tests.Models;

public class DailyQuestTests
{
    private static readonly DateOnly Day = new(2026, 11, 19);

    private static User NewUser() => new("u1", "ani", "x", 0, new DateTime(2026, 1, 1));

    private static SleepQuest Quest(int target = 3, int reward = 30, int progress = 0, bool claimed = false)
        => new("q1", "u1", "desc", target, reward, Day, progress, claimed);

    [Fact]
    public void UpdateProgress_Accumulates()
    {
        var q = Quest(target: 10);
        q.UpdateProgress(3);
        q.UpdateProgress(4);
        Assert.Equal(7, q.CurrentProgress);
        Assert.False(q.CheckCompletion());
    }

    [Fact]
    public void UpdateProgress_ClampsToTarget()
    {
        var q = Quest(target: 5);
        q.UpdateProgress(100);
        Assert.Equal(5, q.CurrentProgress);
        Assert.True(q.CheckCompletion());
    }

    [Fact]
    public void UpdateProgress_Negative_Throws()
    {
        var q = Quest();
        Assert.Throws<ArgumentOutOfRangeException>(() => q.UpdateProgress(-1));
        Assert.Equal(0, q.CurrentProgress);
    }

    [Fact]
    public void UpdateProgress_IntMax_DoesNotOverflow()
    {
        var q = Quest(target: 5, progress: 3);
        q.UpdateProgress(int.MaxValue);
        Assert.Equal(5, q.CurrentProgress);
    }

    [Fact]
    public void CheckCompletion_BoundaryAtTarget()
    {
        Assert.False(Quest(target: 3, progress: 2).CheckCompletion());
        Assert.True(Quest(target: 3, progress: 3).CheckCompletion());
    }

    [Fact]
    public void Claim_BeforeCompletion_ReturnsFalseAndGivesNothing()
    {
        var user = NewUser();
        var q = Quest(target: 3, progress: 2);
        Assert.False(q.Claim(user));
        Assert.False(q.IsClaimed);
        Assert.Equal(0, user.TotalEcoKoin);
    }

    [Fact]
    public void Claim_WhenComplete_AddsRewardAndMarksClaimed()
    {
        var user = NewUser();
        var q = Quest(target: 3, reward: 30, progress: 3);
        Assert.True(q.Claim(user));
        Assert.True(q.IsClaimed);
        Assert.Equal(30, user.TotalEcoKoin);
    }

    [Fact]
    public void Claim_Twice_OnlyPaysOnce()
    {
        var user = NewUser();
        var q = Quest(target: 1, reward: 30, progress: 1);
        Assert.True(q.Claim(user));
        Assert.False(q.Claim(user));
        Assert.Equal(30, user.TotalEcoKoin);
    }

    [Fact]
    public void Claim_AlreadyClaimedRehydratedQuest_ReturnsFalse()
    {
        var user = NewUser();
        var q = Quest(target: 1, reward: 30, progress: 1, claimed: true);
        Assert.False(q.Claim(user));
        Assert.Equal(0, user.TotalEcoKoin);
    }

    [Fact]
    public void Claim_NullUser_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Quest(1, progress: 1).Claim(null!));
    }

    [Theory]
    [InlineData(QuestType.Sleep, typeof(SleepQuest))]
    [InlineData(QuestType.PowerSaver, typeof(PowerSaverQuest))]
    [InlineData(QuestType.WeatherAdaptive, typeof(WeatherAdaptiveQuest))]
    public void Create_ReturnsMatchingSubclassWithAllFields(QuestType type, Type expected)
    {
        var q = DailyQuest.Create(type, "q9", "u9", "d", 7, 11, Day, 4, true);

        Assert.IsType(expected, q);
        Assert.Equal(type, q.Type);
        Assert.Equal("q9", q.QuestId);
        Assert.Equal("u9", q.UserId);
        Assert.Equal("d", q.Description);
        Assert.Equal(7, q.TargetValue);
        Assert.Equal(11, q.RewardCoin);
        Assert.Equal(Day, q.QuestDate);
        Assert.Equal(4, q.CurrentProgress);
        Assert.True(q.IsClaimed);
    }

    [Fact]
    public void Create_UnknownType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DailyQuest.Create((QuestType)99, "q", "u", "d", 1, 1, Day, 0, false));
    }

    [Fact]
    public void CreateDefaultsFor_ReturnsThreeConfiguredQuests()
    {
        var quests = DailyQuest.CreateDefaultsFor("u1", Day);

        Assert.Equal(3, quests.Count);
        Assert.All(quests, q =>
        {
            Assert.Equal("u1", q.UserId);
            Assert.Equal(Day, q.QuestDate);
            Assert.Equal(0, q.CurrentProgress);
            Assert.False(q.IsClaimed);
            Assert.False(string.IsNullOrWhiteSpace(q.Description));
        });
        Assert.Equal(3, quests.Select(q => q.QuestId).Distinct().Count());

        var sleep = Assert.Single(quests, q => q.Type == QuestType.Sleep);
        Assert.Equal((1, 30), (sleep.TargetValue, sleep.RewardCoin));
        var power = Assert.Single(quests, q => q.Type == QuestType.PowerSaver);
        Assert.Equal((240, 20), (power.TargetValue, power.RewardCoin));
        var weather = Assert.Single(quests, q => q.Type == QuestType.WeatherAdaptive);
        Assert.Equal((1, 15), (weather.TargetValue, weather.RewardCoin));
    }

    [Fact]
    public void CreateDefaultsFor_GeneratesNewIdsEachCall()
    {
        var a = DailyQuest.CreateDefaultsFor("u1", Day).Select(q => q.QuestId);
        var b = DailyQuest.CreateDefaultsFor("u1", Day).Select(q => q.QuestId);
        Assert.Empty(a.Intersect(b));
    }

    [Theory]
    [InlineData(QuestType.Sleep, QuestEventKind.SleepEntered)]
    [InlineData(QuestType.PowerSaver, QuestEventKind.PowerSaverMinutes)]
    [InlineData(QuestType.WeatherAdaptive, QuestEventKind.WeatherRecommendationApplied)]
    public void Evaluate_CountsOnlyItsOwnEventKind(QuestType type, QuestEventKind own)
    {
        var q = DailyQuest.Create(type, "q", "u", "d", 10, 5, Day, 0, false);

        Assert.Equal(6, q.Evaluate(new QuestEvent(own, 6)));
        Assert.Equal(1, q.Evaluate(new QuestEvent(own)));
        foreach (var other in Enum.GetValues<QuestEventKind>().Where(k => k != own))
            Assert.Equal(0, q.Evaluate(new QuestEvent(other, 6)));
    }

    [Fact]
    public void Evaluate_DoesNotMutateProgress()
    {
        var q = Quest(target: 5);
        q.Evaluate(new QuestEvent(QuestEventKind.SleepEntered, 3));
        Assert.Equal(0, q.CurrentProgress);
    }

    [Fact]
    public void Polymorphism_EvaluateThenUpdateProgress_CompletesQuestAndClaims()
    {
        var user = NewUser();
        DailyQuest q = new PowerSaverQuest("q", "u1", "d", 240, 20, Day);

        q.UpdateProgress(q.Evaluate(new QuestEvent(QuestEventKind.PowerSaverMinutes, 120)));
        Assert.False(q.CheckCompletion());
        q.UpdateProgress(q.Evaluate(new QuestEvent(QuestEventKind.PowerSaverMinutes, 120)));

        Assert.True(q.Claim(user));
        Assert.Equal(20, user.TotalEcoKoin);
    }
}
