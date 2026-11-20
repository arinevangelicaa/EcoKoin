namespace EcoKoin.Models
{
    public enum QuestEventKind
    {
        /// <summary>PC went to sleep / idle-lock while left unattended.</summary>
        SleepEntered,
        /// <summary>Minutes a power-saving scheme was active (Amount = minutes).</summary>
        PowerSaverMinutes,
        /// <summary>A weather-based recommendation was applied.</summary>
        WeatherRecommendationApplied
    }
}
