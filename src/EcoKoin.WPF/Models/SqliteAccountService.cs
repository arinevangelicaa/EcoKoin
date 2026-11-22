using System.IO;
using EcoKoin.Models;
using EcoKoin.Services;
using Microsoft.Data.Sqlite;

namespace EcoKoin.WPF.Models
{
    internal sealed class SqliteAccountService(string databasePath)
    {
        private const int UniqueConstraint = 2067;
        private readonly object _initializationLock = new();
        private SqliteRepository? _repository;

        internal SqliteRepository GetRepository()
        {
            lock (_initializationLock)
            {
                if (_repository is not null)
                    return _repository;

                var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath };
                var repository = new SqliteRepository(connectionString.ToString());
                repository.InitializeDatabase();
                _repository = repository;
                return repository;
            }
        }

        public static SqliteAccountService CreateDefault() => new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EcoKoin", "ecokoin.db"));

        public Task<bool> RegisterAsync(string name, string email, string password) => Task.Run(() =>
        {
            var user = User.Register(NormalizeEmail(email), password, displayName: name);
            try
            {
                GetRepository().SaveUser(user);
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == UniqueConstraint)
            {
                return false;
            }
        });

        public Task<AuthUser?> LoginAsync(string email, string password, bool rememberMe) => Task.Run<AuthUser?>(() =>
        {
            string username = NormalizeEmail(email);
            var user = GetRepository().GetUserByUsername(username);
            if (user is null || !user.Login(username, password))
                return null;

            return new AuthUser
            {
                UserId = user.UserId,
                Email = username,
                Username = user.DisplayName,
                TotalEcoKoin = user.TotalEcoKoin
            };
        });

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
