using EcoKoin.Services;
using Npgsql;

namespace EcoKoin.Core.Tests.Support;

/// <summary>
/// Runs the provider-neutral dashboard/quest/analytics contract against a real PostgreSQL server.
/// Same setup as <see cref="PostgresRepositoryContractTests"/>: set <c>ECOKOIN_TEST_PG_CONNECTION</c> to the connection
/// string of a disposable database whose user may create schemas; without it the whole suite is skipped.
/// Every test gets its own schema (<c>t_&lt;guid&gt;</c>) that is dropped afterwards.
/// </summary>
public sealed class PostgresRepositoryDashboardContractTests : RepositoryDashboardContractTests
{
    // Hosted servers (e.g. Supabase's session pooler) allow only a handful of connections, so every pool
    // used by the tests is kept small. Extra concurrent callers simply queue on the pool.
    private const int MaxPoolSize = 3;

    private static readonly string? AdminConnectionString = ReadConnectionString();

    private string? _schema;
    private PostgresRepository? _repository;

    protected override string? SkipReason => AdminConnectionString is null
        ? $"Set {PostgresRepositoryContractTests.ConnectionVariable} to run the PostgreSQL contract tests."
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

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            SearchPath = _schema,
            MaxPoolSize = MaxPoolSize,
            MinPoolSize = 0
        };
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

    private static string? ReadConnectionString()
    {
        string? value = Environment.GetEnvironmentVariable(PostgresRepositoryContractTests.ConnectionVariable)?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        return new NpgsqlConnectionStringBuilder(value) { MaxPoolSize = 2, MinPoolSize = 0 }.ConnectionString;
    }
}
