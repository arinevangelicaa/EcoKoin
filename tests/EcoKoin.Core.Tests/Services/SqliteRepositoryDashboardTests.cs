using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

/// <summary>Repository queries/writes backing the real-data dashboard.</summary>
public class SqliteRepositoryDashboardTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private readonly TempDb _db = new();
    private SqliteRepository Repo => _db.Repository;

    public void Dispose() => _db.Dispose();

    private User SaveNewUser(string id = "u1", string name = "hermione", int coins = 0)
    {
        var user = new User(id, name, "1.salt.hash", coins, new DateTime(2026, 1, 2, 3, 4, 5));
        Repo.SaveUser(user);
        return user;
    }

    private DailyQuest SaveQuest(string userId, QuestType type, string id, DateOnly date, int target, int reward,
                                 int progress = 0, bool claimed = false)
    {
        var quest = DailyQuest.Create(type, id, userId, "q", target, reward, date, progress, claimed);
        Repo.SaveQuestProgress(quest);
        return quest;
    }

    private static PowerSession Session(string id, string userId, DateTime start, DateTime? end, double watt = 10)
        => new(id, userId, "scheme-power-saver", start, end, watt);

    // ---- EnsureDailyQuests under concurrency -----------------------------------------------

    [Fact]
    public async Task EnsureDailyQuests_ConcurrentCallers_AllSucceedAndCreateTheDefaultsOnce()
    {
        SaveNewUser();

        // A fresh day each round, callers released at the same instant: this is the first launch of a day,
        // when the quest list, the Power Saver tracker and quest events all call EnsureDailyQuests at once.
        for (int day = 0; day < 6; day++)
        {
            var date = Today.AddDays(day);
            using var start = new ManualResetEventSlim(false);
            var calls = Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
            {
                start.Wait();
                return Repo.EnsureDailyQuests("u1", date);
            })).ToArray();
            start.Set();
            var results = await Task.WhenAll(calls);

            Assert.All(results, quests => Assert.Equal(3, quests.Count));
            var expected = results[0].Select(q => q.QuestId).OrderBy(id => id).ToList();
            Assert.Equal(3, expected.Distinct().Count());
            Assert.All(results, quests => Assert.Equal(expected, quests.Select(q => q.QuestId).OrderBy(id => id)));
        }
    }

    // ---- GetPowerSessions ----------------------------------------------------------------

    [Fact]
    public void GetPowerSessions_ReturnsHalfOpenRangeOrderedIncludingOpenSessions()
    {
        SaveNewUser();
        SaveNewUser("u2", "other");
        var d = new DateTime(2026, 11, 20, 0, 0, 0);
        Repo.SavePowerSession(Session("late", "u1", d.AddHours(10), d.AddHours(11)));
        Repo.SavePowerSession(Session("early", "u1", d.AddHours(1), d.AddHours(2)));
        Repo.SavePowerSession(Session("open", "u1", d.AddHours(5), null));
        Repo.SavePowerSession(Session("atFrom", "u1", d, d.AddHours(1)));
        Repo.SavePowerSession(Session("atTo", "u1", d.AddDays(1), d.AddDays(1).AddHours(1)));
        Repo.SavePowerSession(Session("before", "u1", d.AddSeconds(-1), d));
        Repo.SavePowerSession(Session("foreign", "u2", d.AddHours(3), d.AddHours(4)));

        var result = Repo.GetPowerSessions("u1", d, d.AddDays(1));

        Assert.Equal(new[] { "atFrom", "early", "open", "late" }, result.Select(s => s.SessionId));
        Assert.Null(result.Single(s => s.SessionId == "open").EndTime);
        Assert.Equal(d.AddHours(2), result.Single(s => s.SessionId == "early").EndTime);
    }

    [Fact]
    public void GetPowerSessions_EmptyOrInvertedRange_ReturnsEmpty()
    {
        SaveNewUser();
        var d = new DateTime(2026, 11, 20, 8, 0, 0);
        Repo.SavePowerSession(Session("s", "u1", d, d.AddHours(1)));

        Assert.Empty(Repo.GetPowerSessions("u1", d, d));
        Assert.Empty(Repo.GetPowerSessions("u1", d.AddDays(1), d));
        Assert.Empty(Repo.GetPowerSessions("nobody", d.AddDays(-1), d.AddDays(1)));
    }

    // ---- GetTotalEcoKoin -----------------------------------------------------------------

    [Fact]
    public void GetTotalEcoKoin_ReturnsBalanceOrZeroForMissingUser()
    {
        SaveNewUser(coins: 42);

        Assert.Equal(42, Repo.GetTotalEcoKoin("u1"));
        Assert.Equal(0, Repo.GetTotalEcoKoin("missing"));
    }

    // ---- GetKoinEarnedOn / PerDay --------------------------------------------------------

    [Fact]
    public void GetKoinEarnedOn_SumsOnlyClaimedQuestsOfThatUserAndDate()
    {
        SaveNewUser();
        SaveNewUser("u2", "other");
        SaveQuest("u1", QuestType.Sleep, "a", Today, 1, 30, 1, claimed: true);
        SaveQuest("u1", QuestType.PowerSaver, "b", Today, 240, 20, 240, claimed: true);
        SaveQuest("u1", QuestType.WeatherAdaptive, "c", Today, 1, 15, 1, claimed: false);
        SaveQuest("u1", QuestType.Sleep, "d", Today.AddDays(-1), 1, 30, 1, claimed: true);
        SaveQuest("u2", QuestType.Sleep, "e", Today, 1, 30, 1, claimed: true);

        Assert.Equal(50, Repo.GetKoinEarnedOn("u1", Today));
        Assert.Equal(30, Repo.GetKoinEarnedOn("u1", Today.AddDays(-1)));
        Assert.Equal(0, Repo.GetKoinEarnedOn("u1", Today.AddDays(1)));
        Assert.Equal(0, Repo.GetKoinEarnedOn("missing", Today));
    }

    [Fact]
    public void GetKoinEarnedPerDay_FillsGapsWithZeroAndIsAscendingInclusive()
    {
        SaveNewUser();
        SaveNewUser("u2", "other");
        SaveQuest("u1", QuestType.Sleep, "a", Today.AddDays(-4), 1, 30, 1, claimed: true);
        SaveQuest("u1", QuestType.PowerSaver, "b", Today.AddDays(-4), 240, 20, 240, claimed: true);
        SaveQuest("u1", QuestType.WeatherAdaptive, "c", Today.AddDays(-2), 1, 15, 1, claimed: false);
        SaveQuest("u1", QuestType.Sleep, "d", Today, 1, 30, 1, claimed: true);
        SaveQuest("u1", QuestType.Sleep, "outside", Today.AddDays(-10), 1, 30, 1, claimed: true);
        SaveQuest("u2", QuestType.Sleep, "foreign", Today, 1, 30, 1, claimed: true);

        var result = Repo.GetKoinEarnedPerDay("u1", Today.AddDays(-4), Today);

        Assert.Equal(5, result.Count);
        Assert.Equal(Enumerable.Range(0, 5).Select(i => Today.AddDays(i - 4)), result.Select(r => r.Date));
        Assert.Equal(new[] { 50, 0, 0, 0, 30 }, result.Select(r => r.Koin));
    }

    [Fact]
    public void GetKoinEarnedPerDay_SingleDayEmptyAndInvertedRanges()
    {
        SaveNewUser();

        var single = Repo.GetKoinEarnedPerDay("u1", Today, Today);
        Assert.Equal((Today, 0), Assert.Single(single));

        Assert.Empty(Repo.GetKoinEarnedPerDay("u1", Today, Today.AddDays(-1)));
        Assert.Equal(7, Repo.GetKoinEarnedPerDay("nobody", Today.AddDays(-6), Today).Count);
    }

    // ---- TryClaimQuest -------------------------------------------------------------------

    [Theory]
    [InlineData(QuestType.Sleep, 1, 30)]
    [InlineData(QuestType.WeatherAdaptive, 1, 15)]
    [InlineData(QuestType.PowerSaver, 240, 20)]
    public void TryClaimQuest_CompletedQuestOfAnyType_ClaimsAndAddsRewardOnce(QuestType type, int target, int reward)
    {
        SaveNewUser(coins: 5);
        SaveQuest("u1", type, "q", Today, target, reward, target);

        Assert.True(Repo.TryClaimQuest("u1", "q", Today));
        Assert.False(Repo.TryClaimQuest("u1", "q", Today));

        Assert.Equal(5 + reward, Repo.GetTotalEcoKoin("u1"));
        Assert.True(Repo.EnsureDailyQuests("u1", Today).Single(q => q.QuestId == "q").IsClaimed);
        Assert.Equal(reward, Repo.GetKoinEarnedOn("u1", Today));
    }

    [Fact]
    public void TryClaimQuest_BeforeCompletion_ReturnsFalseAndChangesNothing()
    {
        SaveNewUser(coins: 5);
        SaveQuest("u1", QuestType.PowerSaver, "q", Today, 240, 20, 239);

        Assert.False(Repo.TryClaimQuest("u1", "q", Today));

        Assert.Equal(5, Repo.GetTotalEcoKoin("u1"));
        Assert.False(Repo.EnsureDailyQuests("u1", Today).Single().IsClaimed);
    }

    [Fact]
    public void TryClaimQuest_WrongUserWrongDateOrUnknownQuest_ReturnsFalse()
    {
        SaveNewUser();
        SaveNewUser("u2", "other");
        SaveQuest("u1", QuestType.Sleep, "q", Today, 1, 30, 1);

        Assert.False(Repo.TryClaimQuest("u2", "q", Today));
        Assert.False(Repo.TryClaimQuest("u1", "q", Today.AddDays(1)));
        Assert.False(Repo.TryClaimQuest("u1", "missing", Today));

        Assert.Equal(0, Repo.GetTotalEcoKoin("u1"));
        Assert.Equal(0, Repo.GetTotalEcoKoin("u2"));
        Assert.False(Repo.EnsureDailyQuests("u1", Today).Single().IsClaimed);
    }

    [Fact]
    public void TryClaimQuest_ConcurrentClaims_CreditExactlyOnce()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.Sleep, "q", Today, 1, 30, 1);

        int claims = Enumerable.Range(0, 8).AsParallel().Count(_ => Repo.TryClaimQuest("u1", "q", Today));

        Assert.Equal(1, claims);
        Assert.Equal(30, Repo.GetTotalEcoKoin("u1"));
    }

    // ---- AddQuestProgress ----------------------------------------------------------------

    [Fact]
    public void AddQuestProgress_AddsAndReturnsNewProgress()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.Sleep, "s", Today, 3, 30);

        Assert.Equal(1, Repo.AddQuestProgress("u1", QuestType.Sleep, Today, 1));
        Assert.Equal(2, Repo.AddQuestProgress("u1", QuestType.Sleep, Today, 1));
        Assert.Equal(2, Repo.EnsureDailyQuests("u1", Today).Single().CurrentProgress);
    }

    [Fact]
    public void AddQuestProgress_ClampsToTarget()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.WeatherAdaptive, "w", Today, 1, 15);

        Assert.Equal(1, Repo.AddQuestProgress("u1", QuestType.WeatherAdaptive, Today, 99));
        Assert.Equal(1, Repo.AddQuestProgress("u1", QuestType.WeatherAdaptive, Today, int.MaxValue));
        Assert.Equal(1, Repo.EnsureDailyQuests("u1", Today).Single().CurrentProgress);
    }

    [Fact]
    public void AddQuestProgress_OnlyTouchesMatchingTypeUserAndDate()
    {
        SaveNewUser();
        SaveNewUser("u2", "other");
        SaveQuest("u1", QuestType.Sleep, "s", Today, 5, 30);
        SaveQuest("u1", QuestType.WeatherAdaptive, "w", Today, 5, 15);
        SaveQuest("u1", QuestType.Sleep, "yesterday", Today.AddDays(-1), 5, 30);
        SaveQuest("u2", QuestType.Sleep, "other", Today, 5, 30);

        Assert.Equal(2, Repo.AddQuestProgress("u1", QuestType.Sleep, Today, 2));

        var todays = Repo.EnsureDailyQuests("u1", Today).ToDictionary(q => q.QuestId);
        Assert.Equal(2, todays["s"].CurrentProgress);
        Assert.Equal(0, todays["w"].CurrentProgress);
        Assert.Equal(0, Repo.EnsureDailyQuests("u1", Today.AddDays(-1)).Single(q => q.QuestId == "yesterday").CurrentProgress);
        Assert.Equal(0, Repo.EnsureDailyQuests("u2", Today).Single(q => q.QuestId == "other").CurrentProgress);
    }

    [Fact]
    public void AddQuestProgress_MissingOrClaimedQuest_ReturnsMinusOne()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.Sleep, "s", Today, 1, 30, 1, claimed: true);

        Assert.Equal(-1, Repo.AddQuestProgress("u1", QuestType.Sleep, Today, 1));
        Assert.Equal(-1, Repo.AddQuestProgress("u1", QuestType.WeatherAdaptive, Today, 1));
        Assert.Equal(-1, Repo.AddQuestProgress("u1", QuestType.Sleep, Today.AddDays(1), 1));
        Assert.Equal(-1, Repo.AddQuestProgress("nobody", QuestType.Sleep, Today, 1));
    }

    [Fact]
    public void AddQuestProgress_PowerSaverIsRejected()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.PowerSaver, "p", Today, 240, 20);

        Assert.Throws<ArgumentException>(() => Repo.AddQuestProgress("u1", QuestType.PowerSaver, Today, 1));
        Assert.Equal(0, Repo.EnsureDailyQuests("u1", Today).Single().CurrentProgress);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AddQuestProgress_NonPositiveAmount_Throws(int amount)
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.Sleep, "s", Today, 1, 30);

        Assert.Throws<ArgumentOutOfRangeException>(() => Repo.AddQuestProgress("u1", QuestType.Sleep, Today, amount));
    }

    [Fact]
    public void AddQuestProgress_ThenTryClaimQuest_EndToEnd()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.Sleep, "s", Today, 1, 30);

        Assert.False(Repo.TryClaimQuest("u1", "s", Today));
        Repo.AddQuestProgress("u1", QuestType.Sleep, Today, 1);
        Assert.True(Repo.TryClaimQuest("u1", "s", Today));
        Assert.Equal(30, Repo.GetTotalEcoKoin("u1"));
    }

    // ---- GetOrCreatePet ------------------------------------------------------------------

    [Fact]
    public void GetOrCreatePet_BrandNewUserWithNoQuests_CreatesLemasPet()
    {
        SaveNewUser();

        var pet = Repo.GetOrCreatePet("u1", Today);

        Assert.Equal("u1", pet.UserId);
        Assert.False(string.IsNullOrEmpty(pet.PetId));
        Assert.Equal(PetState.Lemas, pet.CurrentState);
        Assert.Equal(0, pet.HappinessLevel);
        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM ECO_PETS WHERE user_id = 'u1'"));
    }

    [Fact]
    public void GetOrCreatePet_IsIdempotentAndKeepsTheSamePetId()
    {
        SaveNewUser();

        var first = Repo.GetOrCreatePet("u1", Today);
        var second = Repo.GetOrCreatePet("u1", Today);

        Assert.Equal(first.PetId, second.PetId);
        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM ECO_PETS"));
    }

    [Fact]
    public void GetOrCreatePet_UsesExistingPetRow()
    {
        var user = SaveNewUser();
        user.Pet = new EcoPet("pet-1", "u1", 77, PetState.Segar);
        Repo.SaveUser(user);

        var pet = Repo.GetOrCreatePet("u1", Today);

        Assert.Equal("pet-1", pet.PetId);
        Assert.Equal(PetState.Lemas, pet.CurrentState); // recomputed from today's claimed koin (0)
        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM ECO_PETS"));
    }

    [Theory]
    [InlineData(0, PetState.Lemas, 0)]
    [InlineData(9, PetState.Lemas, 27)]
    [InlineData(10, PetState.Normal, 30)]
    [InlineData(29, PetState.Normal, 87)]
    [InlineData(30, PetState.Segar, 90)]
    [InlineData(65, PetState.Segar, 100)]
    public void GetOrCreatePet_StateFollowsKoinClaimedToday_AndIsPersisted(int koin, PetState expected, int happiness)
    {
        SaveNewUser();
        // A claimed quest of another day must not count.
        SaveQuest("u1", QuestType.Sleep, "yesterday", Today.AddDays(-1), 1, 30, 1, claimed: true);
        if (koin > 0)
            SaveQuest("u1", QuestType.WeatherAdaptive, "today", Today, 1, koin, 1, claimed: true);
        SaveQuest("u1", QuestType.PowerSaver, "unclaimed", Today, 240, 20, 240, claimed: false);

        var pet = Repo.GetOrCreatePet("u1", Today);

        Assert.Equal(expected, pet.CurrentState);
        Assert.Equal(happiness, pet.HappinessLevel);

        var reloaded = Repo.GetUserByUsername("hermione")!.Pet!;
        Assert.Equal(pet.PetId, reloaded.PetId);
        Assert.Equal(expected, reloaded.CurrentState);
        Assert.Equal(happiness, reloaded.HappinessLevel);
    }

    [Fact]
    public void GetOrCreatePet_RefreshesStateWhenMoreKoinAreClaimed()
    {
        SaveNewUser();
        SaveQuest("u1", QuestType.Sleep, "s", Today, 1, 30, 1);
        Assert.Equal(PetState.Lemas, Repo.GetOrCreatePet("u1", Today).CurrentState);

        Assert.True(Repo.TryClaimQuest("u1", "s", Today));

        Assert.Equal(PetState.Segar, Repo.GetOrCreatePet("u1", Today).CurrentState);
    }
}
