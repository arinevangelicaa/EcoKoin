using EcoKoin.Models;

namespace EcoKoin.Services
{
    /// <summary>
    /// Turns app signals (a roughly once-a-minute power-saver tick, and sleep/resume system events) into
    /// POWER_SESSIONS rows so <see cref="CarbonFinancialReport"/> has real data.
    /// IMPORTANT: every WattSaved value written here is an ESTIMATE derived from
    /// <see cref="PowerOptions.BaselineWatt"/> (same model as <see cref="PowerMonitorService.CalculateWattSaved"/>),
    /// not a measurement. Thread-safe: the WPF app may call from a timer thread and a system-event thread.
    /// </summary>
    public class PowerSessionRecorder
    {
        public const string PowerSaverSchemeId = "scheme-power-saver";
        public const string BalancedSchemeId = "scheme-balanced";

        /// <summary>A tick further than this after the previous one starts a new session instead of extending.</summary>
        private static readonly TimeSpan MaxTickGap = TimeSpan.FromSeconds(150);
        private static readonly TimeSpan MinSleepDuration = TimeSpan.FromMinutes(1);
        private const int FallbackPowerSaverCpuMax = 50;

        private sealed record OpenSession(string SessionId, DateTime Start, DateTime LastTick);

        private readonly SqliteRepository _repository;
        private readonly IClock _clock;
        private readonly PowerOptions _options;
        private readonly object _gate = new();
        private readonly Dictionary<string, OpenSession> _open = new();
        private int? _powerSaverCpuMax;

        public PowerSessionRecorder(SqliteRepository repository, IClock clock, PowerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(clock);
            _repository = repository;
            _clock = clock;
            _options = options ?? new PowerOptions();
        }

        /// <summary>
        /// Call about once a minute. When <paramref name="powerSaverActive"/> is true the current power-saver run is
        /// extended (same session_id, end_time = now) if the previous tick was at most 150 s ago, otherwise a new
        /// session starts. The row is always written with a non-null end_time (heartbeat) so a crash loses at most
        /// about a minute and the report counts it. WattSaved is an ESTIMATE: BaselineWatt * (100 - cpu_max_performance) / 100
        /// of the "scheme-power-saver" scheme (50 if the scheme is not found).
        /// When false or null the run is forgotten (already persisted) and false is returned.
        /// Returns true when a row was written.
        /// </summary>
        public bool RecordPowerSaverTick(string userId, bool? powerSaverActive)
        {
            lock (_gate)
            {
                if (powerSaverActive != true)
                {
                    _open.Remove(userId);
                    return false;
                }

                DateTime now = _clock.Now;
                double wattSaved = _options.BaselineWatt * (100 - GetPowerSaverCpuMax()) / 100.0;

                string sessionId;
                DateTime start;
                if (_open.TryGetValue(userId, out var open) && now >= open.LastTick && now - open.LastTick <= MaxTickGap)
                {
                    sessionId = open.SessionId;
                    start = open.Start;
                }
                else
                {
                    sessionId = Guid.NewGuid().ToString();
                    start = now;
                }

                _repository.SavePowerSession(new PowerSession(sessionId, userId, PowerSaverSchemeId, start, now, wattSaved));
                _open[userId] = new OpenSession(sessionId, start, now);
                return true;
            }
        }

        /// <summary>
        /// Saves one closed "scheme-balanced" session for a suspend/resume cycle lasting at least one minute.
        /// WattSaved is an ESTIMATE: BaselineWatt * 0.9 (the idle/sleep saving used by <see cref="PowerMonitorService"/>).
        /// Returns false (nothing written) for shorter or inverted intervals.
        /// </summary>
        public bool RecordSleep(string userId, DateTime suspendedAt, DateTime resumedAt)
        {
            if (resumedAt < suspendedAt || resumedAt - suspendedAt < MinSleepDuration)
                return false;

            lock (_gate)
            {
                _repository.SavePowerSession(new PowerSession(
                    Guid.NewGuid().ToString(), userId, BalancedSchemeId, suspendedAt, resumedAt, _options.BaselineWatt * 0.9));
                return true;
            }
        }

        private int GetPowerSaverCpuMax()
        {
            if (_powerSaverCpuMax is { } cached)
                return cached;

            int value = FallbackPowerSaverCpuMax;
            foreach (var scheme in _repository.GetPowerSchemes())
            {
                if (scheme.SchemeId == PowerSaverSchemeId)
                {
                    value = scheme.CpuMaxPerformance;
                    break;
                }
            }

            _powerSaverCpuMax = value;
            return value;
        }
    }
}
