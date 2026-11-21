using System;
using System.IO;
using System.Text.Json;

namespace EcoKoin.WPF.Models
{
    public static class FrontendSession
    {
        private class SessionData
        {
            public string Email { get; set; } = "";
            public DateTime ExpiresAtUtc { get; set; }
        }

        private static readonly string SessionPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EcoKoin",
            "demo-session.json");

        public static string? Email { get; private set; }

        public static bool IsLoggedIn =>
            !string.IsNullOrWhiteSpace(Email);

        public static bool TryRestore()
        {
            Email = null;

            try
            {
                if (!File.Exists(SessionPath))
                    return false;

                var data = JsonSerializer.Deserialize<SessionData>(
                    File.ReadAllText(SessionPath));

                if (data == null ||
                    string.IsNullOrWhiteSpace(data.Email) ||
                    data.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    DeleteSavedSession();
                    return false;
                }

                Email = data.Email;
                return true;
            }
            catch
            {
                DeleteSavedSession();
                return false;
            }
        }

        public static void Login(string email, bool rememberMe)
        {
            Email = email.Trim();

            DeleteSavedSession();

            if (!rememberMe)
                return;

            try
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(SessionPath)!);

                var data = new SessionData
                {
                    Email = Email!,
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
                };

                File.WriteAllText(
                    SessionPath,
                    JsonSerializer.Serialize(data));
            }
            catch
            {
                // Login demo tetap berjalan dalam memori.
            }
        }

        public static void Logout()
        {
            Email = null;
            DeleteSavedSession();
        }

        private static void DeleteSavedSession()
        {
            try
            {
                if (File.Exists(SessionPath))
                    File.Delete(SessionPath);
            }
            catch
            {
                // Jangan membuat aplikasi crash.
            }
        }
    }
}
