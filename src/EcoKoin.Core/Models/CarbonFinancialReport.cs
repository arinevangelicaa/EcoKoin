namespace EcoKoin.Models
{
    /// <summary>
    /// Computed from PowerSessions (not a table). Spec: docs/03-domain-model.md (CarbonFinancialReport).
    /// kWh = sum(WattSaved * DurationHours) / 1000; CO2 = kWh * EmissionFactor; Rupiah = kWh * Tariff.
    /// Only closed sessions (EndTime != null) count.
    /// </summary>
    public class CarbonFinancialReport
    {
        private readonly ReportOptions _options;
        private readonly DateOnly _today;
        private readonly List<PowerSession> _sessions;

        public CarbonFinancialReport(IEnumerable<PowerSession> sessions, DateOnly today, ReportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(sessions);
            _options = options ?? new ReportOptions();
            _today = today;
            _sessions = sessions.Where(s => s.EndTime is not null).ToList();

            TotalWattSaved = _sessions.Sum(s => s.WattSaved);
            TotalKwhSaved = _sessions.Sum(KwhOf);
            TotalCo2Reduced = TotalKwhSaved * _options.EmissionFactorKgPerKwh;
            TotalRupiahSaved = TotalKwhSaved * _options.TariffRupiahPerKwh;
        }

        /// <summary>Sum of <see cref="PowerSession.WattSaved"/> over closed sessions (watts, as stored on each session).</summary>
        public double TotalWattSaved { get; }
        public double TotalKwhSaved { get; }
        public double TotalCo2Reduced { get; }
        public double TotalRupiahSaved { get; }

        /// <summary>Exactly 7 points, oldest to newest, ending at <c>today</c>; days without sessions have KwhSaved = 0.</summary>
        public IReadOnlyList<DailyPoint> GenerateWeeklyChart()
        {
            var byDay = _sessions
                .GroupBy(s => DateOnly.FromDateTime(s.StartTime))
                .ToDictionary(g => g.Key, g => g.Sum(KwhOf));

            var points = new List<DailyPoint>(7);
            for (int offset = 6; offset >= 0; offset--)
            {
                var date = _today.AddDays(-offset);
                points.Add(new DailyPoint(date, byDay.GetValueOrDefault(date)));
            }
            return points;
        }

        /// <summary>kg CO2 for <paramref name="wattHours"/> Wh. Negative input returns 0.</summary>
        public double CalculateCo2(double wattHours)
        {
            return Math.Max(0, wattHours) / 1000.0 * _options.EmissionFactorKgPerKwh;
        }

        /// <summary>Rupiah for <paramref name="wattHours"/> Wh. Negative input returns 0.</summary>
        public double CalculateRupiah(double wattHours)
        {
            return Math.Max(0, wattHours) / 1000.0 * _options.TariffRupiahPerKwh;
        }

        private static double KwhOf(PowerSession s) => s.WattSaved * s.DurationHours / 1000.0;
    }
}
