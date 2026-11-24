using Microsoft.Data.Sqlite;

namespace EcoKoin.Core.Tests.Support;

/// <summary>A throw-away file-based SQLite database with the schema applied (tests only).</summary>
internal sealed class TempDb : IDisposable
{
    private readonly string _dir;

    public TempDb()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ecokoin-tests-" + Guid.NewGuid().ToString("N"));
        DbPath = System.IO.Path.Combine(_dir, "ecokoin.db"); // directory does not exist yet: InitializeDatabase must create it
        ConnectionString = $"Data Source={DbPath}";
        Repository = new SqliteTestRepository(ConnectionString);
        Repository.InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    public string DbPath { get; }
    public string ConnectionString { get; }
    public SqliteTestRepository Repository { get; }

    /// <summary>Opens a raw connection (foreign keys OFF unless the caller enables them).</summary>
    public SqliteConnection OpenRaw()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public long Scalar(string sql)
    {
        using var connection = OpenRaw();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
