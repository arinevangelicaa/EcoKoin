using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Support;

/// <summary>Runs the provider-neutral dashboard/quest/analytics contract against the SQLite test stand-in.</summary>
public sealed class SqliteRepositoryDashboardContractTests : RepositoryDashboardContractTests
{
    private TempDb? _db;

    protected override Task<IAppRepository> CreateAsync()
    {
        _db = new TempDb();
        return Task.FromResult<IAppRepository>(_db.Repository);
    }

    protected override Task CleanupAsync()
    {
        _db?.Dispose();
        return Task.CompletedTask;
    }
}
