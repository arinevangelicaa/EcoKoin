using System;
using System.IO;
using System.Text.Json;

namespace EcoKoin.WPF.Models
{
    // Frontend-only, per-device preferences. No OS power setting is changed.
    public sealed class WelcomePreferences
    {
        public bool HasCompletedOnboarding { get; set; }
        public bool PowerSaverEnabled { get; set; } = true;
        public string SleepDelay { get; set; } = "5 menit";
        public string WeatherLocation { get; set; } = "Catur Tunggal";
    }

    public static class WelcomePreferencesStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EcoKoin", "welcome-preferences.json");

        public static WelcomePreferences Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<WelcomePreferences>(File.ReadAllText(FilePath))
                           ?? new WelcomePreferences();
            }
            catch (Exception)
            {
                // Invalid or inaccessible local file: fall back to safe defaults.
            }
            return new WelcomePreferences();
        }

        public static void Save(WelcomePreferences preferences)
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(
                preferences, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, FilePath, true);
        }
    }
}