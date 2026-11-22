using EcoKoin.Models;
using EcoKoin.Services;
using Microsoft.Data.Sqlite;

namespace EcoKoin.Core.Tests.Services;

public class SqliteRepositoryTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0);

    private readonly TempDb _db = new();
    private SqliteRepository Repo => _db.Repository;

    public void Dispose() => _db.Dispose();

    private static User NewUser(string id = "u1", string name = "hermione", int coins = 0)
        => new(id, name, "1.salt.hash", coins, new DateTime(2026, 1, 2, 3, 4, 5));

    private static SleepQuest NewSleepQuest(string id, string userId, DateOnly date, int progress = 0, bool claimed = false)
        => new(id, userId, "Sleep when away", 1, 30, date, progress, claimed);

    [Fact]
    public void PowerSaverQuest_OnlyCreditsEachMinuteAndClaimOnce()
    {
        var user = NewUser(coins: 5);
        var otherUser = NewUser(id: "u2", name: "second");
        Repo.SaveUser(user);
        Repo.SaveUser(otherUser);
        var quest = new PowerSaverQuest("power-quest", user.UserId, "Power Saver", 240, 20, Today, 239);
        Repo.SaveQuestProgress(quest);

        Assert.False(Repo.TryClaimPowerSaverQuest(user.UserId, quest.QuestId, Today));
        Repo.RecordPowerSaverMinute(otherUser.UserId, quest.QuestId, Today, 500);
        Repo.RecordPowerSaverMinute(user.UserId, quest.QuestId, Today.AddDays(1), 500);
        Assert.Equal(239, Repo.EnsureDailyQuests(user.UserId, Today).Single().CurrentProgress);

        Parallel.For(0, 8, _ => Repo.RecordPowerSaverMinute(user.UserId, quest.QuestId, Today, 500));
        Assert.Equal(240, Repo.EnsureDailyQuests(user.UserId, Today).Single().CurrentProgress);
        Assert.False(Repo.TryClaimPowerSaverQuest(otherUser.UserId, quest.QuestId, Today));

        int claims = Enumerable.Range(0, 8).AsParallel()
            .Count(_ => Repo.TryClaimPowerSaverQuest(user.UserId, quest.QuestId, Today));
        Assert.Equal(1, claims);
        var reopened = new SqliteRepository(_db.ConnectionString);
        Assert.Equal(25, reopened.GetUserByUsername(user.Username)!.TotalEcoKoin);
        Assert.True(reopened.EnsureDailyQuests(user.UserId, Today).Single().IsClaimed);
        Assert.Equal(0, reopened.GetUserByUsername(otherUser.Username)!.TotalEcoKoin);
        reopened.RecordPowerSaverMinute(user.UserId, quest.QuestId, Today, 500);
        reopened.RecordPowerSaverMinute(user.UserId, quest.QuestId, Today, 501);
        reopened.RecordPowerSaverMinute(user.UserId, quest.QuestId, Today, 501);
        Assert.Equal(240, reopened.EnsureDailyQuests(user.UserId, Today).Single().CurrentProgress);
        Assert.False(reopened.TryClaimPowerSaverQuest(user.UserId, quest.QuestId, Today));
        Assert.Equal(25, reopened.GetUserByUsername(user.Username)!.TotalEcoKoin);

        var yesterday = Today.AddDays(-1);
        var yesterdayQuest = reopened.EnsureDailyQuests(user.UserId, yesterday).OfType<PowerSaverQuest>().Single();
        reopened.RecordPowerSaverMinute(user.UserId, yesterdayQuest.QuestId, yesterday, 10);
        Assert.Equal(242L, reopened.GetPowerSaverMinutes(user.UserId, Today.AddDays(-6), Today));
        Assert.Equal(241L, reopened.GetPowerSaverMinutes(user.UserId, Today, Today));
        Assert.Equal(0L, reopened.GetPowerSaverMinutes(otherUser.UserId, Today.AddDays(-6), Today));
    }

    [Fact]
    public void GetPowerSaverMinutes_EmptyUserAndDateRanges_ReturnsZeroAndFiltersCorrectly()
    {
        var user = NewUser("u_fresh", "freshuser");
        Repo.SaveUser(user);

        Assert.Equal(0L, Repo.GetPowerSaverMinutes(user.UserId, Today.AddDays(-6), Today));

        var oldDate = Today.AddDays(-8);
        var oldQuest = Repo.EnsureDailyQuests(user.UserId, oldDate).OfType<PowerSaverQuest>().Single();
        Repo.RecordPowerSaverMinute(user.UserId, oldQuest.QuestId, oldDate, 100);

        Assert.Equal(0L, Repo.GetPowerSaverMinutes(user.UserId, Today.AddDays(-6), Today));
        Assert.Equal(1L, Repo.GetPowerSaverMinutes(user.UserId, Today.AddDays(-10), Today));

        var reopened = new SqliteRepository(_db.ConnectionString);
        Assert.Equal(0L, reopened.GetPowerSaverMinutes(user.UserId, Today.AddDays(-6), Today));
        Assert.Equal(1L, reopened.GetPowerSaverMinutes(user.UserId, Today.AddDays(-10), Today));
    }

    [Fact]
    public void PowerSaverFlow_ProgressToClaimToPersistedBalance_VerifiesFullLifecycle()
    {
        // 1. Akun baru dengan saldo 0
        var user = NewUser("u_demo", "demouser", coins: 0);
        Repo.SaveUser(user);

        var date = Today;
        var quests = Repo.EnsureDailyQuests(user.UserId, date);
        var powerQuest = quests.OfType<PowerSaverQuest>().Single();

        Assert.Equal(0, powerQuest.CurrentProgress);
        Assert.Equal(240, powerQuest.TargetValue);
        Assert.Equal(20, powerQuest.RewardCoin);
        Assert.False(powerQuest.IsClaimed);
        Assert.False(powerQuest.CheckCompletion());
        Assert.Equal(0L, Repo.GetPowerSaverMinutes(user.UserId, date.AddDays(-6), date));

        // 2. Simulasi penambahan menit bertahap hingga 239 menit
        for (int m = 1; m <= 239; m++)
        {
            Repo.RecordPowerSaverMinute(user.UserId, powerQuest.QuestId, date, m);
        }

        powerQuest = Repo.EnsureDailyQuests(user.UserId, date).OfType<PowerSaverQuest>().Single();
        Assert.Equal(239, powerQuest.CurrentProgress);
        Assert.False(powerQuest.CheckCompletion());
        Assert.False(Repo.TryClaimPowerSaverQuest(user.UserId, powerQuest.QuestId, date));
        Assert.Equal(0, Repo.GetUserByUsername(user.Username)!.TotalEcoKoin);
        Assert.Equal(239L, Repo.GetPowerSaverMinutes(user.UserId, date.AddDays(-6), date));

        // 3. Menit ke-240 tercapai (target selesai)
        Repo.RecordPowerSaverMinute(user.UserId, powerQuest.QuestId, date, 240);
        powerQuest = Repo.EnsureDailyQuests(user.UserId, date).OfType<PowerSaverQuest>().Single();
        Assert.Equal(240, powerQuest.CurrentProgress);
        Assert.True(powerQuest.CheckCompletion());

        // 4. Klaim reward dijalankan
        bool claimSuccess = Repo.TryClaimPowerSaverQuest(user.UserId, powerQuest.QuestId, date);
        Assert.True(claimSuccess);

        // Saldo bertambah +20 koin dan quest tercatat diklaim
        var userAfterClaim = Repo.GetUserByUsername(user.Username)!;
        Assert.Equal(20, userAfterClaim.TotalEcoKoin);
        powerQuest = Repo.EnsureDailyQuests(user.UserId, date).OfType<PowerSaverQuest>().Single();
        Assert.True(powerQuest.IsClaimed);

        // 5. Klaim ganda dicegah
        Assert.False(Repo.TryClaimPowerSaverQuest(user.UserId, powerQuest.QuestId, date));
        Assert.Equal(20, Repo.GetUserByUsername(user.Username)!.TotalEcoKoin);

        // 6. Penggunaan terus berlanjut setelah klaim (melebihi 240 menit)
        Repo.RecordPowerSaverMinute(user.UserId, powerQuest.QuestId, date, 241);
        Repo.RecordPowerSaverMinute(user.UserId, powerQuest.QuestId, date, 242);

        // Quest tetap capped di 240, tetapi Analisis melacak durasi aktual 242 menit
        powerQuest = Repo.EnsureDailyQuests(user.UserId, date).OfType<PowerSaverQuest>().Single();
        Assert.Equal(240, powerQuest.CurrentProgress);
        Assert.Equal(242L, Repo.GetPowerSaverMinutes(user.UserId, date.AddDays(-6), date));

        // 7. Aplikasi ditutup dan dibuka kembali (koneksi baru)
        var appReopen = new SqliteRepository(_db.ConnectionString);
        var reloadedUser = appReopen.GetUserByUsername(user.Username)!;
        var reloadedQuest = appReopen.EnsureDailyQuests(user.UserId, date).OfType<PowerSaverQuest>().Single();

        Assert.Equal(20, reloadedUser.TotalEcoKoin);
        Assert.True(reloadedQuest.IsClaimed);
        Assert.Equal(240, reloadedQuest.CurrentProgress);
        Assert.Equal(242L, appReopen.GetPowerSaverMinutes(user.UserId, date.AddDays(-6), date));
        Assert.False(appReopen.TryClaimPowerSaverQuest(user.UserId, powerQuest.QuestId, date));
    }

    // ---- schema / init -------------------------------------------------------------------

    [Fact]
    public void InitializeDatabase_CreatesMissingDirectoryAndFile()
    {
        Assert.True(File.Exists(_db.DbPath));
    }

    [Fact]
    public void InitializeDatabase_IsIdempotent()
    {
        Repo.InitializeDatabase();
        Repo.InitializeDatabase();

        Assert.Equal(5, _db.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
            "('USERS','DAILY_QUESTS','ECO_PETS','POWER_SCHEMES','POWER_SESSIONS')"));
        Assert.Equal(2, _db.Scalar("SELECT COUNT(*) FROM POWER_SCHEMES"));
    }

    [Fact]
    public void GetPowerSchemes_ReturnsSeededSchemesOnceOrderedByCpu()
    {
        Repo.InitializeDatabase();

        var schemes = Repo.GetPowerSchemes();

        Assert.Equal(2, schemes.Count);
        Assert.Equal("Power Saver", schemes[0].SchemeName);
        Assert.Equal(50, schemes[0].CpuMaxPerformance);
        Assert.Equal("Balanced", schemes[1].SchemeName);
        Assert.Equal(80, schemes[1].CpuMaxPerformance);
    }

    [Fact]
    public void InitializeDatabase_AddsDisplayNameToExistingDatabaseWithoutLosingBalance()
    {
        var user = User.Register("legacy@example.test", "test-password");
        user.AddEcoKoin(75);
        Repo.SaveUser(user);

        using (var connection = _db.OpenRaw())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "ALTER TABLE USERS DROP COLUMN display_name;";
            command.ExecuteNonQuery();
        }

        Repo.InitializeDatabase();
        Repo.InitializeDatabase();

        var existing = Repo.GetUserByUsername(user.Username);
        Assert.NotNull(existing);
        Assert.True(existing.Login(user.Username, "test-password"));
        Assert.Equal(75, existing.TotalEcoKoin);
        Assert.Null(existing.DisplayName);

        var registered = User.Register("new@example.test", "test-password", displayName: "Nama Tampilan");
        Repo.SaveUser(registered);
        Assert.Equal("Nama Tampilan", Repo.GetUserByUsername(registered.Username)!.DisplayName);
    }

    // ---- users ---------------------------------------------------------------------------

    [Fact]
    public void GetUserByUsername_UnknownUser_ReturnsNull()
    {
        Assert.Null(Repo.GetUserByUsername("nobody"));
    }

    [Fact]
    public void SaveUser_ThenGet_RoundTripsUserPetAndQuests()
    {
        var user = NewUser(coins: 120);
        user.Pet = new EcoPet("p1", user.UserId, 70, PetState.Segar);
        user.Quests.Add(NewSleepQuest("q1", user.UserId, Today, progress: 1, claimed: true));
        user.Quests.Add(new PowerSaverQuest("q2", user.UserId, "Power saver 4h", 240, 20, Today, 100));
        user.Quests.Add(new WeatherAdaptiveQuest("q3", user.UserId, "Apply weather tip", 1, 15, Today));

        Repo.SaveUser(user);
        var loaded = Repo.GetUserByUsername("hermione");

        Assert.NotNull(loaded);
        Assert.Equal("u1", loaded.UserId);
        Assert.Equal("1.salt.hash", loaded.PasswordHash);
        Assert.Equal(120, loaded.TotalEcoKoin);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), loaded.JoinDate);

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

    [Fact]
    public void SaveUser_WithoutPet_LoadsNullPet()
    {
        Repo.SaveUser(NewUser());

        var loaded = Repo.GetUserByUsername("hermione");

        Assert.NotNull(loaded);
        Assert.Null(loaded.Pet);
        Assert.Empty(loaded.Quests);
    }

    [Fact]
    public void SaveUser_Twice_UpsertsInsteadOfDuplicating()
    {
        Repo.SaveUser(NewUser(coins: 5));
        Repo.SaveUser(NewUser(coins: 9));

        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM USERS"));
        Assert.Equal(9, Repo.GetUserByUsername("hermione")!.TotalEcoKoin);
    }

    [Fact]
    public void SaveUser_DuplicateUsernameForOtherUser_Throws()
    {
        Repo.SaveUser(NewUser("u1", "hermione"));

        Assert.Throws<SqliteException>(() => Repo.SaveUser(NewUser("u2", "hermione")));
        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM USERS"));
    }

    [Fact]
    public void SaveUser_IsAtomic_FailingQuestRollsBackUser()
    {
        var user = NewUser();
        // target_value must be > 0 (CHECK) -> the quest insert fails after the user row was written
        user.Quests.Add(new SleepQuest("q1", user.UserId, "bad", 0, 30, Today));

        Assert.Throws<SqliteException>(() => Repo.SaveUser(user));
        Assert.Equal(0, _db.Scalar("SELECT COUNT(*) FROM USERS"));
    }

    [Fact]
    public void UpdateUserProfile_PersistsCoinsAndPetState()
    {
        var user = NewUser(coins: 10);
        user.Pet = new EcoPet("p1", user.UserId, 10, PetState.Lemas);
        Repo.SaveUser(user);

        var changed = new User(user.UserId, user.Username, user.PasswordHash, 85, user.JoinDate)
        {
            Pet = new EcoPet("p1", user.UserId, 90, PetState.Segar)
        };
        Repo.UpdateUserProfile(changed);

        var loaded = Repo.GetUserByUsername("hermione")!;
        Assert.Equal(85, loaded.TotalEcoKoin);
        Assert.Equal(90, loaded.Pet!.HappinessLevel);
        Assert.Equal(PetState.Segar, loaded.Pet.CurrentState);
    }

    [Fact]
    public void UpdateUserProfile_UnknownUser_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Repo.UpdateUserProfile(NewUser("ghost", "ghost")));
    }

    // ---- quests --------------------------------------------------------------------------

    [Fact]
    public void SaveQuestProgress_UpdatesProgressAndClaimedFlag()
    {
        Repo.SaveUser(NewUser());
        Repo.SaveQuestProgress(NewSleepQuest("q1", "u1", Today));

        Repo.SaveQuestProgress(NewSleepQuest("q1", "u1", Today, progress: 1, claimed: true));

        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
        var quest = Assert.Single(Repo.GetUserByUsername("hermione")!.Quests);
        Assert.Equal(1, quest.CurrentProgress);
        Assert.True(quest.IsClaimed);
    }

    [Fact]
    public void SaveQuestProgress_UnknownUser_ViolatesForeignKey()
    {
        Assert.Throws<SqliteException>(() => Repo.SaveQuestProgress(NewSleepQuest("q1", "ghost", Today)));
    }

    [Fact]
    public void EnsureDailyQuests_CreatesThreeDefaultsOnlyOnce()
    {
        Repo.SaveUser(NewUser());

        var first = Repo.EnsureDailyQuests("u1", Today);
        var second = Repo.EnsureDailyQuests("u1", Today);

        Assert.Equal(3, first.Count);
        Assert.Equal(3, second.Count);
        Assert.Equal(first.Select(q => q.QuestId).OrderBy(x => x), second.Select(q => q.QuestId).OrderBy(x => x));
        Assert.Equal(3, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
        Assert.Contains(first, q => q is SleepQuest { TargetValue: 1, RewardCoin: 30 });
        Assert.Contains(first, q => q is PowerSaverQuest { TargetValue: 240, RewardCoin: 20 });
        Assert.Contains(first, q => q is WeatherAdaptiveQuest { TargetValue: 1, RewardCoin: 15 });
        Assert.All(first, q => Assert.Equal(Today, q.QuestDate));
    }

    [Fact]
    public void EnsureDailyQuests_NewDay_CreatesNewSetAndGetUserReturnsLatestDay()
    {
        Repo.SaveUser(NewUser());
        Repo.EnsureDailyQuests("u1", Today);
        var tomorrow = Today.AddDays(1);

        Repo.EnsureDailyQuests("u1", tomorrow);

        Assert.Equal(6, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
        var loaded = Repo.GetUserByUsername("hermione")!;
        Assert.Equal(3, loaded.Quests.Count);
        Assert.All(loaded.Quests, q => Assert.Equal(tomorrow, q.QuestDate));
    }

    [Fact]
    public void EnsureDailyQuests_UnknownUser_Throws()
    {
        Assert.Throws<SqliteException>(() => Repo.EnsureDailyQuests("ghost", Today));
        Assert.Equal(0, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
    }

    // ---- power sessions ------------------------------------------------------------------

    [Fact]
    public void SavePowerSession_RoundTripsClosedAndRunningSessions()
    {
        Repo.SaveUser(NewUser());
        var closed = new PowerSession("s1", "u1", "scheme-power-saver", Now.AddHours(-3), Now.AddHours(-2), 32.5);
        var running = new PowerSession("s2", "u1", "scheme-balanced", Now.AddMinutes(-10));

        Repo.SavePowerSession(closed);
        Repo.SavePowerSession(running);
        var sessions = Repo.GetWeeklyReportData("u1", Now);

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

    [Fact]
    public void SavePowerSession_SameIdTwice_UpdatesRow()
    {
        Repo.SaveUser(NewUser());
        Repo.SavePowerSession(new PowerSession("s1", "u1", "scheme-power-saver", Now.AddHours(-1)));

        Repo.SavePowerSession(new PowerSession("s1", "u1", "scheme-power-saver", Now.AddHours(-1), Now, 20));

        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM POWER_SESSIONS"));
        var saved = Assert.Single(Repo.GetWeeklyReportData("u1", Now));
        Assert.Equal(Now, saved.EndTime);
        Assert.Equal(20, saved.WattSaved);
    }

    [Fact]
    public void SavePowerSession_UnknownUser_ViolatesForeignKey()
    {
        var session = new PowerSession("s1", "ghost", "scheme-power-saver", Now);

        Assert.Throws<SqliteException>(() => Repo.SavePowerSession(session));
    }

    [Fact]
    public void SavePowerSession_UnknownScheme_ViolatesForeignKey()
    {
        Repo.SaveUser(NewUser());
        var session = new PowerSession("s1", "u1", "no-such-scheme", Now);

        Assert.Throws<SqliteException>(() => Repo.SavePowerSession(session));
    }

    [Fact]
    public void GetWeeklyReportData_FiltersWindowAndUser_OrderedByStart()
    {
        Repo.SaveUser(NewUser("u1", "hermione"));
        Repo.SaveUser(NewUser("u2", "ron"));
        const string scheme = "scheme-power-saver";
        Repo.SavePowerSession(new PowerSession("new", "u1", scheme, Now.AddDays(-1), Now.AddDays(-1).AddHours(1), 10));
        Repo.SavePowerSession(new PowerSession("old", "u1", scheme, Now.AddDays(-8), Now.AddDays(-8).AddHours(1), 10));
        Repo.SavePowerSession(new PowerSession("edge", "u1", scheme, Now.AddDays(-7), Now.AddDays(-7).AddHours(1), 10));
        Repo.SavePowerSession(new PowerSession("mid", "u1", scheme, Now.AddDays(-3), Now.AddDays(-3).AddHours(1), 10));
        Repo.SavePowerSession(new PowerSession("other", "u2", scheme, Now.AddDays(-1), Now.AddDays(-1).AddHours(1), 10));

        var sessions = Repo.GetWeeklyReportData("u1", Now);

        Assert.Equal(new[] { "edge", "mid", "new" }, sessions.Select(s => s.SessionId).ToArray());
    }

    // ---- connection rules ----------------------------------------------------------------

    [Fact]
    public void Repository_EnforcesForeignKeysOnEveryCall()
    {
        // Each repository call opens a fresh connection; all of them must reject orphan rows.
        Assert.Throws<SqliteException>(() => Repo.SaveQuestProgress(NewSleepQuest("q1", "ghost", Today)));
        Assert.Throws<SqliteException>(() => Repo.SaveQuestProgress(NewSleepQuest("q2", "ghost", Today)));
        Assert.Equal(0, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
    }
}
