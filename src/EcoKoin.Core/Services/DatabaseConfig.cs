namespace EcoKoin.Services
{
    /// <summary>
    /// Reads the remote database connection string from the environment so that no credentials
    /// are ever stored in the repository or in a config file.
    /// </summary>
    public static class DatabaseConfig
    {
        /// <summary>Name of the environment variable holding the PostgreSQL (Npgsql) connection string.</summary>
        public const string ConnectionStringVariable = "ECOKOIN_DB_CONNECTION";

        /// <summary>Returns the trimmed connection string, or null when the variable is missing or blank.</summary>
        public static string? GetConnectionString()
        {
            string? value = Environment.GetEnvironmentVariable(ConnectionStringVariable)?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Creates the production repository (<see cref="PostgresRepository"/>) from the environment.
        /// Throws <see cref="InvalidOperationException"/> with an actionable message when the variable is not set.
        /// The connection string itself is never included in messages.
        /// </summary>
        public static IAppRepository CreateFromEnvironment()
        {
            string? connectionString = GetConnectionString();
            if (connectionString is null)
            {
                throw new InvalidOperationException(
                    $"The database connection is not configured. Set the environment variable {ConnectionStringVariable} " +
                    "to a PostgreSQL connection string, e.g. \"Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require\".");
            }

            return new PostgresRepository(connectionString);
        }
    }
}
