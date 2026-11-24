using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Support;

/// <summary>
/// Provider-neutral behaviour of the dashboard, quest and analytics queries of <see cref="IAppRepository"/>
/// (ported from the teammate's SQLite-only <c>SqliteRepositoryDashboardTests</c>). Subclasses supply a fresh, empty,
/// already-initialized repository per test (<see cref="CreateAsync"/>) and clean it up (<see cref="CleanupAsync"/>).
/// </summary>
public abstract class RepositoryDashboardContractTests : IAsyncLifetime
{
    protected static readonly DateOnly Today = new(2026, 11, 20);

    protected IAppRepository Repo { get; private set; } = null!;

    /// <summary>Returns a fresh repository whose database is empty except for the seed data, with InitializeDatabaseAsync already called.</summary>
    protected abstract Task<IAppRepository> CreateAsync();

    /// <summary>Drops/deletes whatever <see cref="CreateAsync"/> created.</summary>
    protected virtual Task CleanupAsync() => Task.CompletedTask;

    /// <summary>When non-null the whole suite is reported as skipped with this reason (e.g. no database configured).</summary>
    protected virtual string? SkipReason => null;

    public async Task InitializeAsync()
    {
        Skip.If(SkipReason is not null, SkipReason);
        Repo = await CreateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Repo is not null)
            await CleanupAsync();
    }

    private async Task<User> SaveNewUserAsync(string id = "u1", string name = "hermione", int coins = 0)
    {
        var user = new User(id, name, "1.salt.hash", coins, new DateTime(2026, 1, 2, 3, 4, 5));
        await Repo.SaveUserAsync(user);
        return user;
    }

    private async Task<DailyQuest> SaveQuestAsync(string userId, QuestType type, string id, DateOnly date, int target, int reward,
                                                  int progress = 0, bool claimed = false)
    {
        var quest = DailyQuest.Create(type, id, userId, "q", target, reward, date, progress, claimed);
        await Repo.SaveQuestProgressAsync(quest);
        return quest;
    }

    private static PowerSession Session(string id, string userId, DateTime start, DateTime? end, double watt = 10)
        => new(id, userId, "scheme-power-saver", start, end, watt);

    // ---- EnsureDailyQuests under concurrency -----------------------------------------------

    [SkippableFact]
    public async Task EnsureDailyQuests_ConcurrentCallers_AllSucceedAndCreateTheDefaultsOnce()
    {
        await SaveNewUserAsync();

        // A fresh day each round, callers released at the same instant: this is the first launch of a day,
        // when the quest list, the Power Saver tracker and quest events all call EnsureDailyQuests at once.
        for (int day = 0; day < 6; day++)
        {
            var date = Today.AddDays(day);
            using var start = new ManualResetEventSlim(false);
            var calls = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await Repo.EnsureDailyQuestsAsync("u1", date);
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

    [SkippableFact]
    public async Task GetPowerSessions_ReturnsHalfOpenRangeOrderedIncludingOpenSessions()
    {
        await SaveNewUserAsync();
        await SaveNewUserAsync("u2", "other");
        var d = new DateTime(2026, 11, 20, 0, 0, 0);
        await Repo.SavePowerSessionAsync(Session("late", "u1", d.AddHours(10), d.AddHours(11)));
        await Repo.SavePowerSessionAsync(Session("early", "u1", d.AddHours(1), d.AddHours(2)));
        await Repo.SavePowerSessionAsync(Session("open", "u1", d.AddHours(5), null));
        await Repo.SavePowerSessionAsync(Session("atFrom", "u1", d, d.AddHours(1)));
        await Repo.SavePowerSessionAsync(Session("atTo", "u1", d.AddDays(1), d.AddDays(1).AddHours(1)));
        await Repo.SavePowerSessionAsync(Session("before", "u1", d.AddSeconds(-1), d));
        await Repo.SavePowerSessionAsync(Session("foreign", "u2", d.AddHours(3), d.AddHours(4)));

        var result = await Repo.GetPowerSessionsAsync("u1", d, d.AddDays(1));

        Assert.Equal(new[] { "atFrom", "early", "open", "late" }, result.Select(s => s.SessionId));
        Assert.Null(result.Single(s => s.SessionId == "open").EndTime);
        Assert.Equal(d.AddHours(2), result.Single(s => s.SessionId == "early").EndTime);
    }

    [SkippableFact]
    public async Task GetPowerSessions_EmptyOrInvertedRange_ReturnsEmpty()
    {
        await SaveNewUserAsync();
        var d = new DateTime(2026, 11, 20, 8, 0, 0);
        await Repo.SavePowerSessionAsync(Session("s", "u1", d, d.AddHours(1)));

        Assert.Empty(await Repo.GetPowerSessionsAsync("u1", d, d));
        Assert.Empty(await Repo.GetPowerSessionsAsync("u1", d.AddDays(1), d));
        Assert.Empty(await Repo.GetPowerSessionsAsync("nobody", d.AddDays(-1), d.AddDays(1)));
    }

    // ---- GetTotalEcoKoin -----------------------------------------------------------------

    [SkippableFact]
    public async Task GetTotalEcoKoin_ReturnsBalanceOrZeroForMissingUser()
    {
        await SaveNewUserAsync(coins: 42);

        Assert.Equal(42, await Repo.GetTotalEcoKoinAsync("u1"));
        Assert.Equal(0, await Repo.GetTotalEcoKoinAsync("missing"));
    }

    // ---- GetKoinEarnedOn / PerDay --------------------------------------------------------

    [SkippableFact]
    public async Task GetKoinEarnedOn_SumsOnlyClaimedQuestsOfThatUserAndDate()
    {
        await SaveNewUserAsync();
        await SaveNewUserAsync("u2", "other");
        await SaveQuestAsync("u1", QuestType.Sleep, "a", Today, 1, 30, 1, claimed: true);
        await SaveQuestAsync("u1", QuestType.PowerSaver, "b", Today, 240, 20, 240, claimed: true);
        await SaveQuestAsync("u1", QuestType.WeatherAdaptive, "c", Today, 1, 15, 1, claimed: false);
        await SaveQuestAsync("u1", QuestType.Sleep, "d", Today.AddDays(-1), 1, 30, 1, claimed: true);
        await SaveQuestAsync("u2", QuestType.Sleep, "e", Today, 1, 30, 1, claimed: true);

        Assert.Equal(50, await Repo.GetKoinEarnedOnAsync("u1", Today));
        Assert.Equal(30, await Repo.GetKoinEarnedOnAsync("u1", Today.AddDays(-1)));
        Assert.Equal(0, await Repo.GetKoinEarnedOnAsync("u1", Today.AddDays(1)));
        Assert.Equal(0, await Repo.GetKoinEarnedOnAsync("missing", Today));
    }

    [SkippableFact]
    public async Task GetKoinEarnedPerDay_FillsGapsWithZeroAndIsAscendingInclusive()
    {
        await SaveNewUserAsync();
        await SaveNewUserAsync("u2", "other");
        await SaveQuestAsync("u1", QuestType.Sleep, "a", Today.AddDays(-4), 1, 30, 1, claimed: true);
        await SaveQuestAsync("u1", QuestType.PowerSaver, "b", Today.AddDays(-4), 240, 20, 240, claimed: true);
        await SaveQuestAsync("u1", QuestType.WeatherAdaptive, "c", Today.AddDays(-2), 1, 15, 1, claimed: false);
        await SaveQuestAsync("u1", QuestType.Sleep, "d", Today, 1, 30, 1, claimed: true);
        await SaveQuestAsync("u1", QuestType.Sleep, "outside", Today.AddDays(-10), 1, 30, 1, claimed: true);
        await SaveQuestAsync("u2", QuestType.Sleep, "foreign", Today, 1, 30, 1, claimed: true);

        var result = await Repo.GetKoinEarnedPerDayAsync("u1", Today.AddDays(-4), Today);

        Assert.Equal(5, result.Count);
        Assert.Equal(Enumerable.Range(0, 5).Select(i => Today.AddDays(i - 4)), result.Select(r => r.Date));
        Assert.Equal(new[] { 50, 0, 0, 0, 30 }, result.Select(r => r.Koin));
    }

    [SkippableFact]
    public async Task GetKoinEarnedPerDay_SingleDayEmptyAndInvertedRanges()
    {
        await SaveNewUserAsync();

        var single = await Repo.GetKoinEarnedPerDayAsync("u1", Today, Today);
        Assert.Equal((Today, 0), Assert.Single(single));

        Assert.Empty(await Repo.GetKoinEarnedPerDayAsync("u1", Today, Today.AddDays(-1)));
        Assert.Equal(7, (await Repo.GetKoinEarnedPerDayAsync("nobody", Today.AddDays(-6), Today)).Count);
    }

    // ---- TryClaimQuest -------------------------------------------------------------------

    [SkippableTheory]
    [InlineData(QuestType.Sleep, 1, 30)]
    [InlineData(QuestType.WeatherAdaptive, 1, 15)]
    [InlineData(QuestType.PowerSaver, 240, 20)]
    public async Task TryClaimQuest_CompletedQuestOfAnyType_ClaimsAndAddsRewardOnce(QuestType type, int target, int reward)
    {
        await SaveNewUserAsync(coins: 5);
        await SaveQuestAsync("u1", type, "q", Today, target, reward, target);

        Assert.True(await Repo.TryClaimQuestAsync("u1", "q", Today));
        Assert.False(await Repo.TryClaimQuestAsync("u1", "q", Today));

        Assert.Equal(5 + reward, await Repo.GetTotalEcoKoinAsync("u1"));
        Assert.True((await Repo.EnsureDailyQuestsAsync("u1", Today)).Single(q => q.QuestId == "q").IsClaimed);
        Assert.Equal(reward, await Repo.GetKoinEarnedOnAsync("u1", Today));
    }

    [SkippableFact]
    public async Task TryClaimQuest_BeforeCompletion_ReturnsFalseAndChangesNothing()
    {
        await SaveNewUserAsync(coins: 5);
        await SaveQuestAsync("u1", QuestType.PowerSaver, "q", Today, 240, 20, 239);

        Assert.False(await Repo.TryClaimQuestAsync("u1", "q", Today));

        Assert.Equal(5, await Repo.GetTotalEcoKoinAsync("u1"));
        Assert.False((await Repo.EnsureDailyQuestsAsync("u1", Today)).Single().IsClaimed);
    }

    [SkippableFact]
    public async Task TryClaimQuest_WrongUserWrongDateOrUnknownQuest_ReturnsFalse()
    {
        await SaveNewUserAsync();
        await SaveNewUserAsync("u2", "other");
        await SaveQuestAsync("u1", QuestType.Sleep, "q", Today, 1, 30, 1);

        Assert.False(await Repo.TryClaimQuestAsync("u2", "q", Today));
        Assert.False(await Repo.TryClaimQuestAsync("u1", "q", Today.AddDays(1)));
        Assert.False(await Repo.TryClaimQuestAsync("u1", "missing", Today));

        Assert.Equal(0, await Repo.GetTotalEcoKoinAsync("u1"));
        Assert.Equal(0, await Repo.GetTotalEcoKoinAsync("u2"));
        Assert.False((await Repo.EnsureDailyQuestsAsync("u1", Today)).Single().IsClaimed);
    }

    [SkippableFact]
    public async Task TryClaimQuest_ConcurrentClaims_CreditExactlyOnce()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.Sleep, "q", Today, 1, 30, 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Repo.TryClaimQuestAsync("u1", "q", Today))));

        Assert.Equal(1, results.Count(claimed => claimed));
        Assert.Equal(30, await Repo.GetTotalEcoKoinAsync("u1"));
    }

    // ---- AddQuestProgress ----------------------------------------------------------------

    [SkippableFact]
    public async Task AddQuestProgress_AddsAndReturnsNewProgress()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.Sleep, "s", Today, 3, 30);

        Assert.Equal(1, await Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today, 1));
        Assert.Equal(2, await Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today, 1));
        Assert.Equal(2, (await Repo.EnsureDailyQuestsAsync("u1", Today)).Single().CurrentProgress);
    }

    [SkippableFact]
    public async Task AddQuestProgress_ClampsToTarget()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.WeatherAdaptive, "w", Today, 1, 15);

        Assert.Equal(1, await Repo.AddQuestProgressAsync("u1", QuestType.WeatherAdaptive, Today, 99));
        Assert.Equal(1, await Repo.AddQuestProgressAsync("u1", QuestType.WeatherAdaptive, Today, int.MaxValue));
        Assert.Equal(1, (await Repo.EnsureDailyQuestsAsync("u1", Today)).Single().CurrentProgress);
    }

    [SkippableFact]
    public async Task AddQuestProgress_OnlyTouchesMatchingTypeUserAndDate()
    {
        await SaveNewUserAsync();
        await SaveNewUserAsync("u2", "other");
        await SaveQuestAsync("u1", QuestType.Sleep, "s", Today, 5, 30);
        await SaveQuestAsync("u1", QuestType.WeatherAdaptive, "w", Today, 5, 15);
        await SaveQuestAsync("u1", QuestType.Sleep, "yesterday", Today.AddDays(-1), 5, 30);
        await SaveQuestAsync("u2", QuestType.Sleep, "other", Today, 5, 30);

        Assert.Equal(2, await Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today, 2));

        var todays = (await Repo.EnsureDailyQuestsAsync("u1", Today)).ToDictionary(q => q.QuestId);
        Assert.Equal(2, todays["s"].CurrentProgress);
        Assert.Equal(0, todays["w"].CurrentProgress);
        Assert.Equal(0, (await Repo.EnsureDailyQuestsAsync("u1", Today.AddDays(-1))).Single(q => q.QuestId == "yesterday").CurrentProgress);
        Assert.Equal(0, (await Repo.EnsureDailyQuestsAsync("u2", Today)).Single(q => q.QuestId == "other").CurrentProgress);
    }

    [SkippableFact]
    public async Task AddQuestProgress_MissingOrClaimedQuest_ReturnsMinusOne()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.Sleep, "s", Today, 1, 30, 1, claimed: true);

        Assert.Equal(-1, await Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today, 1));
        Assert.Equal(-1, await Repo.AddQuestProgressAsync("u1", QuestType.WeatherAdaptive, Today, 1));
        Assert.Equal(-1, await Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today.AddDays(1), 1));
        Assert.Equal(-1, await Repo.AddQuestProgressAsync("nobody", QuestType.Sleep, Today, 1));
    }

    [SkippableFact]
    public async Task AddQuestProgress_PowerSaverIsRejected()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.PowerSaver, "p", Today, 240, 20);

        await Assert.ThrowsAsync<ArgumentException>(() => Repo.AddQuestProgressAsync("u1", QuestType.PowerSaver, Today, 1));
        Assert.Equal(0, (await Repo.EnsureDailyQuestsAsync("u1", Today)).Single().CurrentProgress);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddQuestProgress_NonPositiveAmount_Throws(int amount)
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.Sleep, "s", Today, 1, 30);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today, amount));
    }

    [SkippableFact]
    public async Task AddQuestProgress_ThenTryClaimQuest_EndToEnd()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.Sleep, "s", Today, 1, 30);

        Assert.False(await Repo.TryClaimQuestAsync("u1", "s", Today));
        await Repo.AddQuestProgressAsync("u1", QuestType.Sleep, Today, 1);
        Assert.True(await Repo.TryClaimQuestAsync("u1", "s", Today));
        Assert.Equal(30, await Repo.GetTotalEcoKoinAsync("u1"));
    }

    // ---- GetOrCreatePet ------------------------------------------------------------------

    [SkippableFact]
    public async Task GetOrCreatePet_BrandNewUserWithNoQuests_CreatesLemasPet()
    {
        await SaveNewUserAsync();

        var pet = await Repo.GetOrCreatePetAsync("u1", Today);

        Assert.Equal("u1", pet.UserId);
        Assert.False(string.IsNullOrEmpty(pet.PetId));
        Assert.Equal(PetState.Lemas, pet.CurrentState);
        Assert.Equal(0, pet.HappinessLevel);
        Assert.Equal(pet.PetId, (await Repo.GetUserByUsernameAsync("hermione"))!.Pet!.PetId); // exactly one pet row, persisted
    }

    [SkippableFact]
    public async Task GetOrCreatePet_IsIdempotentAndKeepsTheSamePetId()
    {
        await SaveNewUserAsync();

        var first = await Repo.GetOrCreatePetAsync("u1", Today);
        var second = await Repo.GetOrCreatePetAsync("u1", Today);

        Assert.Equal(first.PetId, second.PetId);
        Assert.Equal(first.PetId, (await Repo.GetUserByUsernameAsync("hermione"))!.Pet!.PetId);
    }

    [SkippableFact]
    public async Task GetOrCreatePet_UsesExistingPetRow()
    {
        var user = await SaveNewUserAsync();
        user.Pet = new EcoPet("pet-1", "u1", 77, PetState.Segar);
        await Repo.SaveUserAsync(user);

        var pet = await Repo.GetOrCreatePetAsync("u1", Today);

        Assert.Equal("pet-1", pet.PetId);
        Assert.Equal(PetState.Lemas, pet.CurrentState); // recomputed from today's claimed koin (0)
        Assert.Equal("pet-1", (await Repo.GetUserByUsernameAsync("hermione"))!.Pet!.PetId); // no second pet was created
    }

    [SkippableTheory]
    [InlineData(0, PetState.Lemas, 0)]
    [InlineData(9, PetState.Lemas, 27)]
    [InlineData(10, PetState.Normal, 30)]
    [InlineData(29, PetState.Normal, 87)]
    [InlineData(30, PetState.Segar, 90)]
    [InlineData(65, PetState.Segar, 100)]
    public async Task GetOrCreatePet_StateFollowsKoinClaimedToday_AndIsPersisted(int koin, PetState expected, int happiness)
    {
        await SaveNewUserAsync();
        // A claimed quest of another day must not count.
        await SaveQuestAsync("u1", QuestType.Sleep, "yesterday", Today.AddDays(-1), 1, 30, 1, claimed: true);
        if (koin > 0)
            await SaveQuestAsync("u1", QuestType.WeatherAdaptive, "today", Today, 1, koin, 1, claimed: true);
        await SaveQuestAsync("u1", QuestType.PowerSaver, "unclaimed", Today, 240, 20, 240, claimed: false);

        var pet = await Repo.GetOrCreatePetAsync("u1", Today);

        Assert.Equal(expected, pet.CurrentState);
        Assert.Equal(happiness, pet.HappinessLevel);

        var reloaded = (await Repo.GetUserByUsernameAsync("hermione"))!.Pet!;
        Assert.Equal(pet.PetId, reloaded.PetId);
        Assert.Equal(expected, reloaded.CurrentState);
        Assert.Equal(happiness, reloaded.HappinessLevel);
    }

    [SkippableFact]
    public async Task GetOrCreatePet_RefreshesStateWhenMoreKoinAreClaimed()
    {
        await SaveNewUserAsync();
        await SaveQuestAsync("u1", QuestType.Sleep, "s", Today, 1, 30, 1);
        Assert.Equal(PetState.Lemas, (await Repo.GetOrCreatePetAsync("u1", Today)).CurrentState);

        Assert.True(await Repo.TryClaimQuestAsync("u1", "s", Today));

        Assert.Equal(PetState.Segar, (await Repo.GetOrCreatePetAsync("u1", Today)).CurrentState);
    }
}
