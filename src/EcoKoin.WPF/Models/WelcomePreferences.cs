using System;
using System.IO;
using System.Text.Json;

namespace EcoKoin.WPF.Models
{
    // Local UI preferences. This class does not change Windows power settings.
    public sealed class WelcomePreferences
    {
        public const string DefaultWeatherLocation = "Yogyakarta";

        public bool HasCompletedOnboarding { get; set; }
        public bool PowerSaverEnabled { get; set; } = true;
        public string PowerProfile { get; set; } = "";
        public string SleepDelay { get; set; } = "5 menit";
        public string WeatherLocation { get; set; } = DefaultWeatherLocation;

        public string GetWeatherLocation() => string.IsNullOrWhiteSpace(WeatherLocation)
            ? DefaultWeatherLocation
            : WeatherLocation.Trim();

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
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(
                preferences, new JsonSerializerOptions { WriteIndented = true });

            if (File.Exists(FilePath))
            {
                string temporaryPath = FilePath + ".tmp";
                File.WriteAllBytes(temporaryPath, data);
                try
                {
                    File.Move(temporaryPath, FilePath, true);
                }
                catch (UnauthorizedAccessException)
                {
                    // Some profiles can write the file but cannot rename or replace it.
                    using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    var previous = new byte[checked((int)stream.Length)];
                    stream.ReadExactly(previous);

                    try
                    {
                        WritePreferences(stream, data);
                    }
                    catch (IOException)
                    {
                        WritePreferences(stream, previous);
                        throw;
                    }
                }
            }
            else
            {
                // The first save creates the file without requiring permission to rename it.
                using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(data);
            }

            PreferencesChanged?.Invoke();
        }

        public static event Action? PreferencesChanged;

        private static void WritePreferences(FileStream stream, byte[] data)
        {
            stream.Position = 0;
            stream.Write(data);
            stream.SetLength(data.Length);
            stream.Flush(flushToDisk: true);
        }
    }
}
