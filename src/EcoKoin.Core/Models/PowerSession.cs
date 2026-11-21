using EcoKoin.Services;

namespace EcoKoin.Models
{
    /// <summary>One usage period of a power scheme. EndTime == null means the session is still running.</summary>
    public class PowerSession
    {
        public PowerSession(string sessionId, string userId, string schemeId, DateTime startTime,
                            DateTime? endTime = null, double wattSaved = 0)
        {
            SessionId = sessionId;
            UserId = userId;
            SchemeId = schemeId;
            StartTime = startTime;
            EndTime = endTime;
            WattSaved = wattSaved;
        }

        public string SessionId { get; }
        public string UserId { get; }
        public string SchemeId { get; }
        public DateTime StartTime { get; }
        public DateTime? EndTime { get; private set; }

        /// <summary>Estimated watts saved (set when the session is closed).</summary>
        public double WattSaved { get; private set; }

        /// <summary>Hours between StartTime and EndTime; 0 while the session is running.</summary>
        public double DurationHours =>
            EndTime is { } end ? (end - StartTime).TotalHours : 0;

        /// <summary>Sets EndTime and WattSaved. Throws InvalidOperationException if already closed or endTime &lt; StartTime.</summary>
        public void Close(DateTime endTime, double wattSaved)
        {
            if (EndTime is not null)
                throw new InvalidOperationException("Session is already closed.");
            if (endTime < StartTime)
                throw new InvalidOperationException("EndTime cannot be earlier than StartTime.");

            EndTime = endTime;
            WattSaved = wattSaved;
        }

        /// <summary>Delegates to <see cref="SqliteRepository.SavePowerSession"/>.</summary>
        public void SaveToDatabase(SqliteRepository repo)
        {
            ArgumentNullException.ThrowIfNull(repo);
            repo.SavePowerSession(this);
        }
    }
}
