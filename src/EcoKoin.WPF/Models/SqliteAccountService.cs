using System.IO;
using System.Text.Json;
using EcoKoin.Models;
using EcoKoin.Services;
using Microsoft.Data.Sqlite;

namespace EcoKoin.WPF.Models
{
    internal sealed class SqliteAccountService(string databasePath)
    {
        private const int UniqueConstraint = 2067;
        private const string SessionFileName = "session.json";
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
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

            // A fresh login always replaces any earlier remembered session.
            DeleteSavedSession();
            if (rememberMe)
                SaveSession(username);

            return ToAuthUser(user);
        });

        /// <summary>Signs in again from the session saved by "Ingat saya"; null when none is valid.</summary>
        public Task<AuthUser?> RestoreSessionAsync() => Task.Run<AuthUser?>(() =>
        {
            var session = ReadSession();
            if (session is null || string.IsNullOrWhiteSpace(session.Email) || session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                DeleteSavedSession();
                return null;
            }

            var user = GetRepository().GetUserByUsername(NormalizeEmail(session.Email));
            if (user is null)
            {
                DeleteSavedSession();
                return null;
            }

            return ToAuthUser(user);
        });

        /// <summary>Forgets the remembered session so the next launch asks for a login.</summary>
        public Task<bool> LogoutAsync() => Task.Run(() =>
        {
            DeleteSavedSession();
            return !File.Exists(SessionPath);
        });

        private string SessionPath =>
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, SessionFileName);

        private void SaveSession(string email)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
                var data = new SavedSession { Email = email, ExpiresAtUtc = DateTime.UtcNow.Add(SessionLifetime) };
                File.WriteAllText(SessionPath, JsonSerializer.Serialize(data));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The login itself succeeded; it is just not remembered for the next launch.
            }
        }

        private SavedSession? ReadSession()
        {
            try
            {
                return File.Exists(SessionPath)
                    ? JsonSerializer.Deserialize<SavedSession>(File.ReadAllText(SessionPath))
                    : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        private void DeleteSavedSession()
        {
            try
            {
                if (File.Exists(SessionPath))
                    File.Delete(SessionPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Reported through LogoutAsync, which checks that the file is really gone.
            }
        }

        private static AuthUser ToAuthUser(User user) => new()
        {
            UserId = user.UserId,
            Email = user.Username,
            Username = user.DisplayName,
            TotalEcoKoin = user.TotalEcoKoin
        };

        private sealed class SavedSession
        {
            public string Email { get; set; } = "";
            public DateTime ExpiresAtUtc { get; set; }
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
