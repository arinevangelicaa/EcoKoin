using System;
using System.Threading.Tasks;

namespace EcoKoin.WPF.Models
{
    public sealed class AuthUser
    {
        public string Email { get; set; } = "";
        public string? Username { get; set; }
        public int? TotalEcoKoin { get; set; }
    }

    public static class AuthBridge
    {
        public static AuthUser? CurrentUser { get; private set; }

        // Diisi oleh integrasi BE.
        public static Func<Task<AuthUser?>>?
            RestoreSessionHandler
        { get; set; }

        public static Func<string, string, bool,
            Task<AuthUser?>>? LoginHandler
        { get; set; }

        public static Func<Task<bool>>?
            LogoutHandler
        { get; set; }

        public static bool IsAuthenticated =>
            CurrentUser != null;

        public static async Task<bool> RestoreAsync()
        {
            CurrentUser = null;

            if (RestoreSessionHandler == null)
                return false;

            try
            {
                var user = await RestoreSessionHandler();

                if (user == null ||
                    string.IsNullOrWhiteSpace(user.Email))
                    return false;

                CurrentUser = user;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> LoginAsync(
            string email,
            string password,
            bool rememberMe)
        {
            if (LoginHandler == null)
                throw new InvalidOperationException(
                    "Layanan login belum terhubung ke backend.");

            var user = await LoginHandler(
                email, password, rememberMe);

            if (user == null ||
                string.IsNullOrWhiteSpace(user.Email))
                return false;

            CurrentUser = user;
            return true;
        }

        public static async Task<bool> LogoutAsync()
        {
            if (LogoutHandler == null)
                throw new InvalidOperationException(
                    "Layanan logout belum terhubung ke backend.");

            bool success = await LogoutHandler();

            if (success)
                CurrentUser = null;

            return success;
        }
    }
}
