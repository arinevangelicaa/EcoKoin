using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Support;

/// <summary>
/// Provider-neutral behaviour every <see cref="IAppRepository"/> must satisfy. Subclasses supply a fresh,
/// empty, already-initialized repository per test (<see cref="CreateAsync"/>) and clean it up (<see cref="CleanupAsync"/>).
/// </summary>
public abstract class RepositoryContractTests : IAsyncLifetime
{
    protected static readonly DateOnly Today = new(2026, 11, 20);
    protected static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0);

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

    protected static User NewUser(string id = "u1", string name = "hermione", int coins = 0, string? displayName = null)
        => new(id, name, "1.salt.hash", coins, new DateTime(2026, 1, 2, 3, 4, 5), displayName);

    protected static SleepQuest NewSleepQuest(string id, string userId, DateOnly date, int progress = 0, bool claimed = false)
        => new(id, userId, "Sleep when away", 1, 30, date, progress, claimed);

    // ---- schema / init -------------------------------------------------------------------

    [SkippableFact]
    public async Task InitializeDatabase_IsIdempotent()
    {
        await Repo.InitializeDatabaseAsync();
        await Repo.InitializeDatabaseAsync();

        Assert.Equal(2, (await Repo.GetPowerSchemesAsync()).Count);
    }

    [SkippableFact]
    public async Task GetPowerSchemes_ReturnsSeededSchemesOnceOrderedByCpu()
    {
        var schemes = await Repo.GetPowerSchemesAsync();

        Assert.Equal(2, schemes.Count);
        Assert.Equal("scheme-power-saver", schemes[0].SchemeId);
        Assert.Equal("Power Saver", schemes[0].SchemeName);
        Assert.Equal(50, schemes[0].CpuMaxPerformance);
        Assert.Equal("scheme-balanced", schemes[1].SchemeId);
        Assert.Equal("Balanced", schemes[1].SchemeName);
        Assert.Equal(80, schemes[1].CpuMaxPerformance);
    }

    // ---- users ---------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetUserByUsername_UnknownUser_ReturnsNull()
    {
        Assert.Null(await Repo.GetUserByUsernameAsync("nobody"));
    }

    [SkippableFact]
    public async Task SaveUser_ThenGet_RoundTripsUserPetAndQuests()
    {
        var user = NewUser(coins: 120, displayName: "Hermione G.");
        user.Pet = new EcoPet("p1", user.UserId, 70, PetState.Segar);
        user.Quests.Add(NewSleepQuest("q1", user.UserId, Today, progress: 1, claimed: true));
        user.Quests.Add(new PowerSaverQuest("q2", user.UserId, "Power saver 4h", 240, 20, Today, 100));
        user.Quests.Add(new WeatherAdaptiveQuest("q3", user.UserId, "Apply weather tip", 1, 15, Today));

        await Repo.SaveUserAsync(user);
        var loaded = await Repo.GetUserByUsernameAsync("hermione");

        Assert.NotNull(loaded);
        Assert.Equal("u1", loaded.UserId);
        Assert.Equal("1.salt.hash", loaded.PasswordHash);
        Assert.Equal(120, loaded.TotalEcoKoin);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), loaded.JoinDate);
        Assert.Equal("Hermione G.", loaded.DisplayName);

        Assert.NotNull(loaded.Pet);
        Assert.Equal("p1", loaded.Pet.PetId);
        Assert.Equal(70, loaded.Pet.HappinessLevel);
        Assert.Equal(PetState.Segar, loaded.Pet.CurrentState);

        Assert.Equal(3, loaded.Quests.Count);
        var sleep = Assert.IsType<SleepQuest>(loaded.Quests[0]);
        Assert.Equal(1, sleep.CurrentProgress);
        Assert.True(sleep.IsClaimed);
        Assert.Equal(Today, sleep.QuestDate);
        var saver = Assert.IsType<PowerSaverQuest>(loaded.Quests[1]);
        Assert.Equal(240, saver.TargetValue);
        Assert.Equal(100, saver.CurrentProgress);
        Assert.False(saver.IsClaimed);
        Assert.IsType<WeatherAdaptiveQuest>(loaded.Quests[2]);
    }

    [SkippableFact]
    public async Task SaveUser_WithoutPet_LoadsNullPet()
    {
        await Repo.SaveUserAsync(NewUser());

        var loaded = await Repo.GetUserByUsernameAsync("hermione");

        Assert.NotNull(loaded);
        Assert.Null(loaded.Pet);
        Assert.Empty(loaded.Quests);
        Assert.Null(loaded.DisplayName);
    }

    [SkippableFact]
    public async Task SaveUser_Twice_UpsertsInsteadOfDuplicating()
    {
        await Repo.SaveUserAsync(NewUser(coins: 5));
        await Repo.SaveUserAsync(NewUser(coins: 9));

        Assert.Equal(9, (await Repo.GetUserByUsernameAsync("hermione"))!.TotalEcoKoin);
    }

    [SkippableFact]
    public async Task SaveUser_NullDisplayName_KeepsStoredDisplayName()
    {
        await Repo.SaveUserAsync(NewUser(displayName: "Hermione G."));
        await Repo.SaveUserAsync(NewUser(coins: 3, displayName: null));

        var loaded = (await Repo.GetUserByUsernameAsync("hermione"))!;
        Assert.Equal("Hermione G.", loaded.DisplayName);
        Assert.Equal(3, loaded.TotalEcoKoin);
    }

    [SkippableFact]
    public async Task SaveUser_DuplicateUsernameForOtherUser_ThrowsDuplicateUsernameException()
    {
        await Repo.SaveUserAsync(NewUser("u1", "hermione", coins: 7));

        var ex = await Assert.ThrowsAsync<DuplicateUsernameException>(() => Repo.SaveUserAsync(NewUser("u2", "hermione")));

        Assert.Equal("hermione", ex.Username);
        var stored = (await Repo.GetUserByUsernameAsync("hermione"))!;
        Assert.Equal("u1", stored.UserId);
        Assert.Equal(7, stored.TotalEcoKoin);
    }

    [SkippableFact]
    public async Task SaveUser_IsAtomic_FailingQuestRollsBackUser()
    {
        var user = NewUser();
        // target_value must be > 0 (CHECK) -> the quest insert fails after the user row was written
        user.Quests.Add(new SleepQuest("q1", user.UserId, "bad", 0, 30, Today));

        await Assert.ThrowsAnyAsync<Exception>(() => Repo.SaveUserAsync(user));

        Assert.Null(await Repo.GetUserByUsernameAsync("hermione"));
    }

    [SkippableFact]
    public async Task UpdateUserProfile_PersistsCoinsAndPetState()
    {
        var user = NewUser(coins: 10);
        user.Pet = new EcoPet("p1", user.UserId, 10, PetState.Lemas);
        await Repo.SaveUserAsync(user);

        var changed = new User(user.UserId, user.Username, user.PasswordHash, 85, user.JoinDate)
        {
            Pet = new EcoPet("p1", user.UserId, 90, PetState.Segar)
        };
        await Repo.UpdateUserProfileAsync(changed);

        var loaded = (await Repo.GetUserByUsernameAsync("hermione"))!;
        Assert.Equal(85, loaded.TotalEcoKoin);
        Assert.Equal(90, loaded.Pet!.HappinessLevel);
        Assert.Equal(PetState.Segar, loaded.Pet.CurrentState);
    }

    [SkippableFact]
    public async Task UpdateUserProfile_UnknownUser_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.UpdateUserProfileAsync(NewUser("ghost", "ghost")));
    }

    // ---- quests --------------------------------------------------------------------------

    [SkippableFact]
    public async Task SaveQuestProgress_UpdatesProgressAndClaimedFlag()
    {
        await Repo.SaveUserAsync(NewUser());
        await Repo.SaveQuestProgressAsync(NewSleepQuest("q1", "u1", Today));

        await Repo.SaveQuestProgressAsync(NewSleepQuest("q1", "u1", Today, progress: 1, claimed: true));

        var quest = Assert.Single((await Repo.GetUserByUsernameAsync("hermione"))!.Quests);
        Assert.Equal(1, quest.CurrentProgress);
        Assert.True(quest.IsClaimed);
    }

    [SkippableFact]
    public async Task SaveQuestProgress_UnknownUser_Throws()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Repo.SaveQuestProgressAsync(NewSleepQuest("q1", "ghost", Today)));
    }

    [SkippableFact]
    public async Task EnsureDailyQuests_CreatesThreeDefaultsOnlyOnce()
    {
        await Repo.SaveUserAsync(NewUser());

        var first = await Repo.EnsureDailyQuestsAsync("u1", Today);
        var second = await Repo.EnsureDailyQuestsAsync("u1", Today);

        Assert.Equal(3, first.Count);
        Assert.Equal(3, second.Count);
        Assert.Equal(first.Select(q => q.QuestId).OrderBy(x => x), second.Select(q => q.QuestId).OrderBy(x => x));
        Assert.Equal(3, (await Repo.GetUserByUsernameAsync("hermione"))!.Quests.Count);
        Assert.Contains(first, q => q is SleepQuest { TargetValue: 1, RewardCoin: 30 });
        Assert.Contains(first, q => q is PowerSaverQuest { TargetValue: 240, RewardCoin: 20 });
        Assert.Contains(first, q => q is WeatherAdaptiveQuest { TargetValue: 1, RewardCoin: 15 });
        Assert.All(first, q => Assert.Equal(Today, q.QuestDate));
    }

    [SkippableFact]
    public async Task EnsureDailyQuests_NewDay_CreatesNewSetAndGetUserReturnsLatestDay()
    {
        await Repo.SaveUserAsync(NewUser());
        var todays = await Repo.EnsureDailyQuestsAsync("u1", Today);
        var tomorrow = Today.AddDays(1);

        var tomorrows = await Repo.EnsureDailyQuestsAsync("u1", tomorrow);

        Assert.Empty(todays.Select(q => q.QuestId).Intersect(tomorrows.Select(q => q.QuestId)));
        var loaded = (await Repo.GetUserByUsernameAsync("hermione"))!;
        Assert.Equal(3, loaded.Quests.Count);
        Assert.All(loaded.Quests, q => Assert.Equal(tomorrow, q.QuestDate));
        Assert.Equal(3, (await Repo.EnsureDailyQuestsAsync("u1", Today)).Count); // old day is still intact
    }

    [SkippableFact]
    public async Task EnsureDailyQuests_UnknownUser_Throws()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Repo.EnsureDailyQuestsAsync("ghost", Today));
    }

    // ---- power sessions ------------------------------------------------------------------

    [SkippableFact]
    public async Task SavePowerSession_RoundTripsClosedAndRunningSessions()
    {
        await Repo.SaveUserAsync(NewUser());
        var closed = new PowerSession("s1", "u1", "scheme-power-saver", Now.AddHours(-3), Now.AddHours(-2), 32.5);
        var running = new PowerSession("s2", "u1", "scheme-balanced", Now.AddMinutes(-10));

        await Repo.SavePowerSessionAsync(closed);
        await Repo.SavePowerSessionAsync(running);
        var sessions = await Repo.GetWeeklyReportDataAsync("u1", Now);

        Assert.Equal(2, sessions.Count);
        Assert.Equal("s1", sessions[0].SessionId);
        Assert.Equal(Now.AddHours(-3), sessions[0].StartTime);
        Assert.Equal(Now.AddHours(-2), sessions[0].EndTime);
        Assert.Equal(32.5, sessions[0].WattSaved);
        Assert.Equal("scheme-power-saver", sessions[0].SchemeId);
        Assert.Equal("s2", sessions[1].SessionId);
        Assert.Null(sessions[1].EndTime);
        Assert.Equal(0, sessions[1].WattSaved);
    }

    [SkippableFact]
    public async Task SavePowerSession_SameIdTwice_UpdatesRow()
    {
        await Repo.SaveUserAsync(NewUser());
        await Repo.SavePowerSessionAsync(new PowerSession("s1", "u1", "scheme-power-saver", Now.AddHours(-1)));

        await Repo.SavePowerSessionAsync(new PowerSession("s1", "u1", "scheme-power-saver", Now.AddHours(-1), Now, 20));

        var saved = Assert.Single(await Repo.GetWeeklyReportDataAsync("u1", Now));
        Assert.Equal(Now, saved.EndTime);
        Assert.Equal(20, saved.WattSaved);
    }

    [SkippableFact]
    public async Task SavePowerSession_UnknownUser_Throws()
    {
        var session = new PowerSession("s1", "ghost", "scheme-power-saver", Now);

        await Assert.ThrowsAnyAsync<Exception>(() => Repo.SavePowerSessionAsync(session));
    }

    [SkippableFact]
    public async Task SavePowerSession_UnknownScheme_Throws()
    {
        await Repo.SaveUserAsync(NewUser());
        var session = new PowerSession("s1", "u1", "no-such-scheme", Now);

        await Assert.ThrowsAnyAsync<Exception>(() => Repo.SavePowerSessionAsync(session));
    }

    [SkippableFact]
    public async Task GetWeeklyReportData_FiltersWindowAndUser_OrderedByStart()
    {
        await Repo.SaveUserAsync(NewUser("u1", "hermione"));
        await Repo.SaveUserAsync(NewUser("u2", "ron"));
        const string scheme = "scheme-power-saver";
        await Repo.SavePowerSessionAsync(new PowerSession("new", "u1", scheme, Now.AddDays(-1), Now.AddDays(-1).AddHours(1), 10));
        await Repo.SavePowerSessionAsync(new PowerSession("old", "u1", scheme, Now.AddDays(-8), Now.AddDays(-8).AddHours(1), 10));
        await Repo.SavePowerSessionAsync(new PowerSession("edge", "u1", scheme, Now.AddDays(-7), Now.AddDays(-7).AddHours(1), 10));
        await Repo.SavePowerSessionAsync(new PowerSession("mid", "u1", scheme, Now.AddDays(-3), Now.AddDays(-3).AddHours(1), 10));
        await Repo.SavePowerSessionAsync(new PowerSession("other", "u2", scheme, Now.AddDays(-1), Now.AddDays(-1).AddHours(1), 10));

        var sessions = await Repo.GetWeeklyReportDataAsync("u1", Now);

        Assert.Equal(new[] { "edge", "mid", "new" }, sessions.Select(s => s.SessionId).ToArray());
    }

    // ---- cancellation / orphans ----------------------------------------------------------

    [SkippableFact]
    public async Task CancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repo.GetPowerSchemesAsync(cts.Token));
    }

    [SkippableFact]
    public async Task OrphanRows_AreRejectedOnEveryCall()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Repo.SaveQuestProgressAsync(NewSleepQuest("q1", "ghost", Today)));
        await Assert.ThrowsAnyAsync<Exception>(() => Repo.SaveQuestProgressAsync(NewSleepQuest("q2", "ghost", Today)));
        await Repo.SaveUserAsync(NewUser());
        Assert.Empty((await Repo.GetUserByUsernameAsync("hermione"))!.Quests);
    }

    // ---- Power Saver and weather quest flows (ported from the teammate's SQLite-only tests) ----

    [SkippableFact]
    public async Task PowerSaverQuest_OnlyCreditsEachMinuteAndClaimOnce()
    {
        var user = NewUser(coins: 5);
        var otherUser = NewUser(id: "u2", name: "second");
        await Repo.SaveUserAsync(user);
        await Repo.SaveUserAsync(otherUser);
        var quest = new PowerSaverQuest("power-quest", user.UserId, "Power Saver", 240, 20, Today, 239);
        await Repo.SaveQuestProgressAsync(quest);

        Assert.False(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, quest.QuestId, Today));
        await Repo.RecordPowerSaverMinuteAsync(otherUser.UserId, quest.QuestId, Today, 500);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, quest.QuestId, Today.AddDays(1), 500);
        Assert.Equal(239, (await Repo.EnsureDailyQuestsAsync(user.UserId, Today)).Single().CurrentProgress);

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Repo.RecordPowerSaverMinuteAsync(user.UserId, quest.QuestId, Today, 500))));
        Assert.Equal(240, (await Repo.EnsureDailyQuestsAsync(user.UserId, Today)).Single().CurrentProgress);
        Assert.False(await Repo.TryClaimPowerSaverQuestAsync(otherUser.UserId, quest.QuestId, Today));

        var claims = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Repo.TryClaimPowerSaverQuestAsync(user.UserId, quest.QuestId, Today))));
        Assert.Equal(1, claims.Count(claimed => claimed));
        Assert.Equal(25, (await Repo.GetUserByUsernameAsync(user.Username))!.TotalEcoKoin);
        Assert.True((await Repo.EnsureDailyQuestsAsync(user.UserId, Today)).Single().IsClaimed);
        Assert.Equal(0, (await Repo.GetUserByUsernameAsync(otherUser.Username))!.TotalEcoKoin);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, quest.QuestId, Today, 500);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, quest.QuestId, Today, 501);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, quest.QuestId, Today, 501);
        Assert.Equal(240, (await Repo.EnsureDailyQuestsAsync(user.UserId, Today)).Single().CurrentProgress);
        Assert.False(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, quest.QuestId, Today));
        Assert.Equal(25, (await Repo.GetUserByUsernameAsync(user.Username))!.TotalEcoKoin);

        var yesterday = Today.AddDays(-1);
        var yesterdayQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, yesterday)).OfType<PowerSaverQuest>().Single();
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, yesterdayQuest.QuestId, yesterday, 10);
        Assert.Equal(242L, await Repo.GetPowerSaverMinutesAsync(user.UserId, Today.AddDays(-6), Today));
        Assert.Equal(241L, await Repo.GetPowerSaverMinutesAsync(user.UserId, Today, Today));
        Assert.Equal(0L, await Repo.GetPowerSaverMinutesAsync(otherUser.UserId, Today.AddDays(-6), Today));
    }

    [SkippableFact]
    public async Task GetPowerSaverMinutes_EmptyUserAndDateRanges_ReturnsZeroAndFiltersCorrectly()
    {
        var user = NewUser("u_fresh", "freshuser");
        await Repo.SaveUserAsync(user);

        Assert.Equal(0L, await Repo.GetPowerSaverMinutesAsync(user.UserId, Today.AddDays(-6), Today));

        var oldDate = Today.AddDays(-8);
        var oldQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, oldDate)).OfType<PowerSaverQuest>().Single();
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, oldQuest.QuestId, oldDate, 100);

        Assert.Equal(0L, await Repo.GetPowerSaverMinutesAsync(user.UserId, Today.AddDays(-6), Today));
        Assert.Equal(1L, await Repo.GetPowerSaverMinutesAsync(user.UserId, Today.AddDays(-10), Today));
    }

    [SkippableFact]
    public async Task PowerSaverFlow_ProgressToClaimToPersistedBalance_VerifiesFullLifecycle()
    {
        // 1. Akun baru dengan saldo 0
        var user = NewUser("u_demo", "demouser", coins: 0);
        await Repo.SaveUserAsync(user);

        var date = Today;
        var quests = await Repo.EnsureDailyQuestsAsync(user.UserId, date);
        var powerQuest = quests.OfType<PowerSaverQuest>().Single();

        Assert.Equal(0, powerQuest.CurrentProgress);
        Assert.Equal(240, powerQuest.TargetValue);
        Assert.Equal(20, powerQuest.RewardCoin);
        Assert.False(powerQuest.IsClaimed);
        Assert.False(powerQuest.CheckCompletion());
        Assert.Equal(0L, await Repo.GetPowerSaverMinutesAsync(user.UserId, date.AddDays(-6), date));

        // 2. Simulasi penambahan menit bertahap hingga 239 menit
        for (int m = 1; m <= 239; m++)
            await Repo.RecordPowerSaverMinuteAsync(user.UserId, powerQuest.QuestId, date, m);

        powerQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, date)).OfType<PowerSaverQuest>().Single();
        Assert.Equal(239, powerQuest.CurrentProgress);
        Assert.False(powerQuest.CheckCompletion());
        Assert.False(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, powerQuest.QuestId, date));
        Assert.Equal(0, (await Repo.GetUserByUsernameAsync(user.Username))!.TotalEcoKoin);
        Assert.Equal(239L, await Repo.GetPowerSaverMinutesAsync(user.UserId, date.AddDays(-6), date));

        // 3. Menit ke-240 tercapai (target selesai)
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, powerQuest.QuestId, date, 240);
        powerQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, date)).OfType<PowerSaverQuest>().Single();
        Assert.Equal(240, powerQuest.CurrentProgress);
        Assert.True(powerQuest.CheckCompletion());

        // 4. Klaim reward dijalankan
        Assert.True(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, powerQuest.QuestId, date));

        // Saldo bertambah +20 koin dan quest tercatat diklaim
        var userAfterClaim = (await Repo.GetUserByUsernameAsync(user.Username))!;
        Assert.Equal(20, userAfterClaim.TotalEcoKoin);
        powerQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, date)).OfType<PowerSaverQuest>().Single();
        Assert.True(powerQuest.IsClaimed);

        // 5. Klaim ganda dicegah
        Assert.False(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, powerQuest.QuestId, date));
        Assert.Equal(20, (await Repo.GetUserByUsernameAsync(user.Username))!.TotalEcoKoin);

        // 6. Penggunaan terus berlanjut setelah klaim (melebihi 240 menit)
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, powerQuest.QuestId, date, 241);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, powerQuest.QuestId, date, 242);

        // Quest tetap capped di 240, tetapi Analisis melacak durasi aktual 242 menit
        powerQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, date)).OfType<PowerSaverQuest>().Single();
        Assert.Equal(240, powerQuest.CurrentProgress);
        Assert.Equal(242L, await Repo.GetPowerSaverMinutesAsync(user.UserId, date.AddDays(-6), date));

        // 7. Aplikasi ditutup dan dibuka kembali: semua data tetap ada di database (setiap panggilan membaca ulang dari DB)
        var reloadedUser = (await Repo.GetUserByUsernameAsync(user.Username))!;
        var reloadedQuest = (await Repo.EnsureDailyQuestsAsync(user.UserId, date)).OfType<PowerSaverQuest>().Single();

        Assert.Equal(20, reloadedUser.TotalEcoKoin);
        Assert.True(reloadedQuest.IsClaimed);
        Assert.Equal(240, reloadedQuest.CurrentProgress);
        Assert.Equal(242L, await Repo.GetPowerSaverMinutesAsync(user.UserId, date.AddDays(-6), date));
        Assert.False(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, powerQuest.QuestId, date));
    }

    [SkippableFact]
    public async Task WeatherAdaptiveQuest_Apply_Claim_And_PetStateLifecycle()
    {
        var user = NewUser("u-weather", "weather_tester", coins: 0);
        await Repo.SaveUserAsync(user);
        var quests = await Repo.EnsureDailyQuestsAsync(user.UserId, Today);
        var powerQuest = quests.OfType<PowerSaverQuest>().Single();

        Assert.Equal(0, await Repo.GetCoinsEarnedTodayAsync(user.UserId, Today));
        Assert.False(await Repo.TryClaimWeatherQuestAsync(user.UserId, Today));

        Assert.True(await Repo.TryApplyWeatherQuestAsync(user.UserId, Today));
        var updatedQuests = await Repo.EnsureDailyQuestsAsync(user.UserId, Today);
        Assert.Equal(1, updatedQuests.OfType<WeatherAdaptiveQuest>().Single().CurrentProgress);

        Assert.True(await Repo.TryClaimWeatherQuestAsync(user.UserId, Today));
        Assert.False(await Repo.TryClaimWeatherQuestAsync(user.UserId, Today));

        var userAfterWeather = (await Repo.GetUserByUsernameAsync(user.Username))!;
        Assert.Equal(15, userAfterWeather.TotalEcoKoin);
        Assert.Equal(15, await Repo.GetCoinsEarnedTodayAsync(user.UserId, Today));
        Assert.NotNull(userAfterWeather.Pet);
        Assert.Equal(PetState.Normal, userAfterWeather.Pet.CurrentState);
        Assert.Equal(45, userAfterWeather.Pet.HappinessLevel);

        // Advance power saver to target and claim
        for (int i = 0; i < 240; i++)
            await Repo.RecordPowerSaverMinuteAsync(user.UserId, powerQuest.QuestId, Today, 1000 + i);

        Assert.True(await Repo.TryClaimPowerSaverQuestAsync(user.UserId, powerQuest.QuestId, Today));
        var userAfterPower = (await Repo.GetUserByUsernameAsync(user.Username))!;
        Assert.Equal(35, userAfterPower.TotalEcoKoin);
        Assert.Equal(35, await Repo.GetCoinsEarnedTodayAsync(user.UserId, Today));
        Assert.NotNull(userAfterPower.Pet);
        Assert.Equal(PetState.Segar, userAfterPower.Pet.CurrentState);
        Assert.Equal(100, userAfterPower.Pet.HappinessLevel);
    }

    [SkippableFact]
    public async Task GetDailyPowerSaverMinutes_ReturnsMinutesPerDateInRange()
    {
        var user = NewUser("u-daily-mins", "daily_user", coins: 0);
        await Repo.SaveUserAsync(user);
        var q1 = (await Repo.EnsureDailyQuestsAsync(user.UserId, Today.AddDays(-2))).OfType<PowerSaverQuest>().Single();
        var q2 = (await Repo.EnsureDailyQuestsAsync(user.UserId, Today)).OfType<PowerSaverQuest>().Single();

        await Repo.RecordPowerSaverMinuteAsync(user.UserId, q1.QuestId, Today.AddDays(-2), 10);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, q1.QuestId, Today.AddDays(-2), 11);
        await Repo.RecordPowerSaverMinuteAsync(user.UserId, q2.QuestId, Today, 20);

        var daily = await Repo.GetDailyPowerSaverMinutesAsync(user.UserId, Today.AddDays(-3), Today);
        Assert.Equal(2, daily[Today.AddDays(-2)]);
        Assert.Equal(1, daily[Today]);
        Assert.False(daily.ContainsKey(Today.AddDays(-1)));
    }
}
