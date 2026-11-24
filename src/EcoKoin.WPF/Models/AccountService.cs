using System.IO;
using System.Text.Json;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.WPF.Models
{
    /// <summary>
    /// Akun pengguna di atas <see cref="IAppRepository"/> (PostgreSQL remote). File sesi "Ingat saya"
    /// hanya menyimpan email dan waktu kedaluwarsa (bukan kata sandi atau hash) dan bukan database.
    /// </summary>
    internal sealed class AccountService
    {
        private const string SessionFileName = "session.json";
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

        private readonly IAppRepository _repository;
        private readonly string _sessionDirectory;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _initialized;

        public AccountService(IAppRepository repository, string? sessionDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(repository);
            _repository = repository;
            _sessionDirectory = sessionDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EcoKoin");
        }

        /// <summary>
        /// Repository yang sudah diinisialisasi (skema dibuat saat startup lewat <see cref="InitializeAsync"/>).
        /// Dipakai oleh halaman Dashboard, Misi, Analisis, dan perekam sesi daya.
        /// </summary>
        internal IAppRepository GetRepository() => _repository;

        /// <summary>
        /// Membuat skema database (idempoten) satu kali. Dipanggil lazily oleh Register/Login
        /// dan secara eksplisit saat startup agar kesalahan koneksi terdeteksi lebih awal.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
                return;

            await _initializationLock.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return;

                await _repository.InitializeDatabaseAsync(ct);
                _initialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        public async Task<bool> RegisterAsync(string name, string email, string password)
        {
            await InitializeAsync();

            var user = User.Register(NormalizeEmail(email), password, displayName: name);
            try
            {
                await _repository.SaveUserAsync(user);
                return true;
            }
            catch (DuplicateUsernameException)
            {
                return false;
            }
        }

        public async Task<AuthUser?> LoginAsync(string email, string password, bool rememberMe)
        {
            await InitializeAsync();

            string username = NormalizeEmail(email);
            var user = await _repository.GetUserByUsernameAsync(username);
            if (user is null || !user.Login(username, password))
                return null;

            // A fresh login always replaces any earlier remembered session.
            DeleteSavedSession();
            if (rememberMe)
                SaveSession(username);

            return ToAuthUser(user);
        }

        /// <summary>Signs in again from the session saved by "Ingat saya"; null when none is valid.</summary>
        public async Task<AuthUser?> RestoreSessionAsync()
        {
            var session = ReadSession();
            if (session is null || string.IsNullOrWhiteSpace(session.Email) || session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                DeleteSavedSession();
                return null;
            }

            await InitializeAsync();

            var user = await _repository.GetUserByUsernameAsync(NormalizeEmail(session.Email));
            if (user is null)
            {
                DeleteSavedSession();
                return null;
            }

            return ToAuthUser(user);
        }

        /// <summary>Forgets the remembered session so the next launch asks for a login.</summary>
        public Task<bool> LogoutAsync()
        {
            DeleteSavedSession();
            return Task.FromResult(!File.Exists(SessionPath));
        }

        private string SessionPath => Path.Combine(_sessionDirectory, SessionFileName);

        private void SaveSession(string email)
        {
            try
            {
                Directory.CreateDirectory(_sessionDirectory);
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
