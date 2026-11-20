using System;
using System.IO;
using System.Text.Json;

namespace EcoKoin.WPF.Models
{
    // Local UI preferences. This class does not change Windows power settings.
    public sealed class WelcomePreferences
    {
        public bool HasCompletedOnboarding { get; set; }
        public bool PowerSaverEnabled { get; set; } = true;
        public string SleepDelay { get; set; } = "5 menit";
        public string WeatherLocation { get; set; } = "Catur Tunggal";

        // Keep these preferences across application restarts.
        public bool WeatherRecommendationsEnabled { get; set; } = true;
        public bool PetDesktopEnabled { get; set; } = true;
        public bool PetReminderEnabled { get; set; } = true;
        public bool MissionReminderEnabled { get; set; } = true;
        public bool ClaimReminderEnabled { get; set; } = true;
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
                // Invalid or inaccessible local data: use safe UI defaults.
            }
            return new WelcomePreferences();
        }

        public static void Save(WelcomePreferences preferences)
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            string temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(
                preferences, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, FilePath, true);
        }
    }
}