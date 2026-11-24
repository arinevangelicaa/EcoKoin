using EcoKoin.Models;
using EcoKoin.Services;
using Microsoft.Data.Sqlite;

namespace EcoKoin.Core.Tests.Support;

/// <summary>SQLite-specific behaviour of the test stand-in (file creation, migration, raw constraint errors).</summary>
public class SqliteTestRepositoryTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private readonly TempDb _db = new();
    private SqliteTestRepository Repo => _db.Repository;

    public void Dispose() => _db.Dispose();

    [Fact]
    public void InitializeDatabase_CreatesMissingDirectoryAndFile()
    {
        Assert.True(File.Exists(_db.DbPath));
    }

    [Fact]
    public async Task InitializeDatabase_CreatesFiveTablesAndTwoSchemes()
    {
        await Repo.InitializeDatabaseAsync();
        await Repo.InitializeDatabaseAsync();

        Assert.Equal(5, _db.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
            "('USERS','DAILY_QUESTS','ECO_PETS','POWER_SCHEMES','POWER_SESSIONS')"));
        Assert.Equal(2, _db.Scalar("SELECT COUNT(*) FROM POWER_SCHEMES"));
    }

    [Fact]
    public async Task InitializeDatabase_AddsDisplayNameToExistingDatabaseWithoutLosingBalance()
    {
        var user = User.Register("legacy@example.test", "test-password");
        user.AddEcoKoin(75);
        await Repo.SaveUserAsync(user);

        using (var connection = _db.OpenRaw())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "ALTER TABLE USERS DROP COLUMN display_name;";
            command.ExecuteNonQuery();
        }

        await Repo.InitializeDatabaseAsync();
        await Repo.InitializeDatabaseAsync();

        var existing = await Repo.GetUserByUsernameAsync(user.Username);
        Assert.NotNull(existing);
        Assert.True(existing.Login(user.Username, "test-password"));
        Assert.Equal(75, existing.TotalEcoKoin);
        Assert.Null(existing.DisplayName);

        var registered = User.Register("new@example.test", "test-password", displayName: "Nama Tampilan");
        await Repo.SaveUserAsync(registered);
        Assert.Equal("Nama Tampilan", (await Repo.GetUserByUsernameAsync(registered.Username))!.DisplayName);
    }

    [Fact]
    public async Task SaveUser_DuplicateUsername_KeepsOnlyFirstRow()
    {
        await Repo.SaveUserAsync(new User("u1", "hermione", "1.salt.hash", 0, DateTime.UnixEpoch));

        await Assert.ThrowsAsync<DuplicateUsernameException>(
            () => Repo.SaveUserAsync(new User("u2", "hermione", "1.salt.hash", 0, DateTime.UnixEpoch)));

        Assert.Equal(1, _db.Scalar("SELECT COUNT(*) FROM USERS"));
    }

    [Fact]
    public async Task SaveUser_FailingQuest_LeavesNoUserRow()
    {
        var user = new User("u1", "hermione", "1.salt.hash", 0, DateTime.UnixEpoch);
        user.Quests.Add(new SleepQuest("q1", user.UserId, "bad", 0, 30, Today));

        await Assert.ThrowsAsync<SqliteException>(() => Repo.SaveUserAsync(user));

        Assert.Equal(0, _db.Scalar("SELECT COUNT(*) FROM USERS"));
    }

    [Fact]
    public async Task ForeignKeyViolation_SurfacesAsSqliteException_AndLeavesNoRows()
    {
        await Assert.ThrowsAsync<SqliteException>(
            () => Repo.SaveQuestProgressAsync(new SleepQuest("q1", "ghost", "x", 1, 30, Today)));
        await Assert.ThrowsAsync<SqliteException>(() => Repo.EnsureDailyQuestsAsync("ghost", Today));

        Assert.Equal(0, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
    }

    [Fact]
    public async Task EnsureDailyQuests_CreatesSixRowsForTwoDays()
    {
        await Repo.SaveUserAsync(new User("u1", "hermione", "1.salt.hash", 0, DateTime.UnixEpoch));
        await Repo.EnsureDailyQuestsAsync("u1", Today);
        await Repo.EnsureDailyQuestsAsync("u1", Today.AddDays(1));

        Assert.Equal(6, _db.Scalar("SELECT COUNT(*) FROM DAILY_QUESTS"));
    }
}
