using EcoKoin.Services;
using Npgsql;

namespace EcoKoin.Core.Tests.Support;

/// <summary>
/// Runs the provider-neutral repository contract against a real PostgreSQL server.
/// Set <c>ECOKOIN_TEST_PG_CONNECTION</c> to an Npgsql connection string of a disposable database whose user may
/// create schemas, e.g. <c>Host=127.0.0.1;Port=5432;Database=ecokoin_test;Username=ecokoin;Password=...</c>.
/// Without the variable the whole suite is reported as skipped.
/// Every test gets its own schema (<c>t_&lt;guid&gt;</c>) that is dropped afterwards, so tests are isolated and
/// nothing is left behind.
/// </summary>
public sealed class PostgresRepositoryContractTests : RepositoryContractTests
{
    public const string ConnectionVariable = "ECOKOIN_TEST_PG_CONNECTION";

    private static readonly string? AdminConnectionString = ReadConnectionString();

    private string? _schema;
    private PostgresRepository? _repository;

    protected override string? SkipReason => AdminConnectionString is null
        ? $"Set {ConnectionVariable} to run the PostgreSQL contract tests."
        : null;

    protected override async Task<IAppRepository> CreateAsync()
    {
        _schema = "t_" + Guid.NewGuid().ToString("N");

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE SCHEMA {_schema};", admin);
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString) { SearchPath = _schema };
        _repository = new PostgresRepository(builder.ConnectionString);
        await _repository.InitializeDatabaseAsync();
        return _repository;
    }

    protected override async Task CleanupAsync()
    {
        if (_repository is not null)
            await _repository.DisposeAsync();

        if (_schema is not null && AdminConnectionString is not null)
        {
            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {_schema} CASCADE;", admin);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ---- PostgreSQL-specific behaviour ---------------------------------------------------

    [SkippableFact]
    public async Task EnsureDailyQuests_ConcurrentCallers_CreateExactlyThreeQuests()
    {
        await Repo.SaveUserAsync(NewUser());

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Repo.EnsureDailyQuestsAsync("u1", Today)));

        Assert.All(results, quests => Assert.Equal(3, quests.Count));
        Assert.Equal(results[0].Select(q => q.QuestId), results[7].Select(q => q.QuestId));
        var loaded = await Repo.GetUserByUsernameAsync("hermione");
        Assert.Equal(3, loaded!.Quests.Count);
    }

    [SkippableFact]
    public async Task InitializeDatabase_ConcurrentCallers_DoNotFail()
    {
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Repo.InitializeDatabaseAsync()));

        Assert.Equal(2, (await Repo.GetPowerSchemesAsync()).Count);
    }

    [SkippableTheory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public async Task Timestamps_AreStoredAsWallClockRegardlessOfKind(DateTimeKind kind)
    {
        var join = DateTime.SpecifyKind(new DateTime(2026, 3, 4, 5, 6, 7), kind);
        await Repo.SaveUserAsync(new EcoKoin.Models.User("u1", "hermione", "1.salt.hash", 0, join));

        var loaded = await Repo.GetUserByUsernameAsync("hermione");

        Assert.Equal(new DateTime(2026, 3, 4, 5, 6, 7), loaded!.JoinDate);
    }

    private static string? ReadConnectionString()
    {
        string? value = Environment.GetEnvironmentVariable(ConnectionVariable)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
