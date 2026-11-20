using EcoKoin.Models;

namespace EcoKoin.Services
{
    /// <summary>Spec: docs/03-domain-model.md (PowerMonitorService).</summary>
    public class PowerMonitorService
    {
        private readonly IIdleTimeProvider _idle;
        private readonly IPowerCommandRunner _runner;
        private readonly IClock _clock;
        private readonly PowerOptions _options;

        public PowerMonitorService(IIdleTimeProvider idle, IPowerCommandRunner runner, IClock clock, PowerOptions? options = null)
        {
            _idle = idle;
            _runner = runner;
            _clock = clock;
            _options = options ?? new PowerOptions();
        }

        public bool IsIdle { get; private set; }
        public double IdleThresholdMinutes => _options.IdleThresholdMinutes;
        public PowerScheme? CurrentScheme { get; private set; }

        /// <summary>IsIdle = idle time &gt;= IdleThresholdMinutes. Returns IsIdle.</summary>
        public bool DetectIdleState()
        {
            IsIdle = _idle.GetIdleTime().TotalMinutes >= _options.IdleThresholdMinutes;
            return IsIdle;
        }

        /// <summary>scheme.Activate(runner) then CurrentScheme = scheme.</summary>
        public void ApplyPowerScheme(PowerScheme scheme)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            scheme.Activate(_runner);
            CurrentScheme = scheme;
        }

        /// <summary>
        /// Estimate: BaselineWatt * (100 - CpuMaxPerformance) / 100 for CurrentScheme (0 if none);
        /// if IsIdle, add BaselineWatt * 0.9 (sleep saving).
        /// </summary>
        public double CalculateWattSaved()
        {
            double saved = 0;
            if (CurrentScheme is { } scheme)
                saved += _options.BaselineWatt * (100 - scheme.CpuMaxPerformance) / 100.0;
            if (IsIdle)
                saved += _options.BaselineWatt * 0.9;
            return Math.Max(0, saved);
        }
    }
}
