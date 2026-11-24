using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.WPF.Models
{
    internal sealed class AccountService(IAppRepository repository)
    {
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _initialized;

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

                await repository.InitializeDatabaseAsync(ct);
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
                await repository.SaveUserAsync(user);
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
            var user = await repository.GetUserByUsernameAsync(username);
            if (user is null || !user.Login(username, password))
                return null;

            return new AuthUser
            {
                Email = username,
                Username = user.DisplayName,
                TotalEcoKoin = user.TotalEcoKoin
            };
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
