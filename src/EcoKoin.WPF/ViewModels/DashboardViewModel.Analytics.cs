using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.WPF.ViewModels
{
    // OWNER: analytics agent. Weekly analytics card of the dashboard (labels, summary values, koin of the week).
    public sealed partial class DashboardViewModel
    {
        private string[] _grafikLabels = AnalyticsFormat.WeekLabels(DateOnly.FromDateTime(DateTime.Today));
        private string _grafikRentangDisplay = AnalyticsFormat.WeekRange(DateOnly.FromDateTime(DateTime.Today));

        // X-axis labels of the seven-day chart (abbreviated weekday, oldest first, last = today).
        public string GrafikLabel0 => _grafikLabels[0];
        public string GrafikLabel1 => _grafikLabels[1];
        public string GrafikLabel2 => _grafikLabels[2];
        public string GrafikLabel3 => _grafikLabels[3];
        public string GrafikLabel4 => _grafikLabels[4];
        public string GrafikLabel5 => _grafikLabels[5];
        public string GrafikLabel6 => _grafikLabels[6];

        // Date range covered by the chart, e.g. "17 Nov - 23 Nov".
        public string GrafikRentangDisplay
        {
            get => _grafikRentangDisplay;
            private set => SetProperty(ref _grafikRentangDisplay, value);
        }

        internal void ShowWeeklyAnalytics(WeekAnalytics week)
        {
            _grafikLabels = AnalyticsFormat.WeekLabels(week.Today);
            for (int i = 0; i < 7; i++) Notify($"GrafikLabel{i}");
            GrafikRentangDisplay = AnalyticsFormat.WeekRange(week.Today);

            EnergiDisplay = week.Kwh.ToString("0.##", IndonesianCulture) + " kWh";
            EmisiDisplay = week.Co2Kg.ToString("0.##", IndonesianCulture) + " kg";
            BiayaDisplay = "Rp" + week.Rupiah.ToString("N0", IndonesianCulture);
            SetGrafikMingguan(week.DailyKwh, isDemo: false);
            if (!week.HasSessions)
                GrafikStatusDisplay = "Belum ada data";

            KoinMingguanDisplay = week.Koin > 0
                ? $"+{week.Koin.ToString("N0", IndonesianCulture)} koin dalam 7 hari terakhir"
                : "Belum ada koin dalam 7 hari terakhir";
        }

        internal void ShowWeeklyAnalyticsError()
        {
            GrafikStatusDisplay = "Data belum dapat dimuat";
        }
    }

    internal sealed record WeekAnalytics(DateOnly Today, double[] DailyKwh, double Kwh, double Co2Kg,
        double Rupiah, int Koin, bool HasSessions);

    // One analysis period (7 days / this month / this year) ready for AnalysisView.UpdatePeriodData.
    internal sealed record PeriodAnalytics(List<AnalysisDataPoint> Points, double Kwh, double Co2Kg, double Rupiah,
        double PowerSaverKwh, double SleepKwh, double DurationHours);

    internal static class AnalyticsFormat
    {
        private static readonly string[] Weekdays = { "Min", "Sen", "Sel", "Rab", "Kam", "Jum", "Sab" };
        private static readonly string[] Months =
            { "Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des" };

        public static string Weekday(DateOnly date) => Weekdays[(int)date.DayOfWeek];

        public static string Month(int month) => Months[month - 1];

        public static string[] WeekLabels(DateOnly today)
        {
            var labels = new string[7];
            for (int i = 0; i < 7; i++)
                labels[i] = Weekday(today.AddDays(i - 6));
            return labels;
        }

        public static string WeekRange(DateOnly today)
        {
            var from = today.AddDays(-6);
            return $"{from.Day} {Month(from.Month)} - {today.Day} {Month(today.Month)}";
        }
    }

    // Database reads for the analytics. Call from a background thread; the result is plain data.
    internal static class AnalyticsLoader
    {
        private static DateTime StartOf(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

        public static WeekAnalytics LoadWeek(SqliteRepository repository, string userId, DateOnly today)
        {
            var from = today.AddDays(-6);
            var sessions = repository.GetPowerSessions(userId, StartOf(from), StartOf(today.AddDays(1)));
            var report = new CarbonFinancialReport(sessions, today);
            double[] daily = report.GenerateWeeklyChart().Select(p => p.KwhSaved).ToArray();
            int koin = repository.GetKoinEarnedPerDay(userId, from, today).Sum(d => d.Koin);
            return new WeekAnalytics(today, daily, report.TotalKwhSaved, report.TotalCo2Reduced,
                report.TotalRupiahSaved, koin, sessions.Any(s => s.EndTime is not null));
        }

        // Index 0 = last 7 days, 1 = this month, 2 = this year; null when the period has no closed session.
        public static PeriodAnalytics?[] LoadPeriods(SqliteRepository repository, string userId, DateOnly today)
        {
            var yearStart = new DateOnly(today.Year, 1, 1);
            var weekStart = today.AddDays(-6);
            var earliest = weekStart < yearStart ? weekStart : yearStart;
            var sessions = repository.GetPowerSessions(userId, StartOf(earliest), StartOf(today.AddDays(1)))
                .Where(s => s.EndTime is not null)
                .ToList();

            DateOnly DayOf(PowerSession s) => DateOnly.FromDateTime(s.StartTime);

            var week = sessions.Where(s => DayOf(s) >= weekStart).ToList();
            var weekPoints = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i))
                .Select(day => (AnalyticsFormat.Weekday(day), week.Where(s => DayOf(s) == day).ToList()));

            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var month = sessions.Where(s => DayOf(s) >= monthStart).ToList();
            var monthPoints = Enumerable.Range(1, today.Day).Select(d => monthStart.AddDays(d - 1))
                .Select(day => (day.Day.ToString(CultureInfo.InvariantCulture),
                    month.Where(s => DayOf(s) == day).ToList()));

            var year = sessions.Where(s => DayOf(s).Year == today.Year).ToList();
            var yearPoints = Enumerable.Range(1, today.Month)
                .Select(m => (AnalyticsFormat.Month(m), year.Where(s => DayOf(s).Month == m).ToList()));

            return new[]
            {
                Build(week, weekPoints, today),
                Build(month, monthPoints, today),
                Build(year, yearPoints, today)
            };
        }

        private static PeriodAnalytics? Build(List<PowerSession> all,
            IEnumerable<(string Label, List<PowerSession> Sessions)> buckets, DateOnly today)
        {
            if (all.Count == 0)
                return null;

            var points = buckets.Select(b =>
            {
                var report = new CarbonFinancialReport(b.Sessions, today);
                return new AnalysisDataPoint
                {
                    Label = b.Label,
                    Co2SavedKg = report.TotalCo2Reduced,
                    MoneySavedRp = report.TotalRupiahSaved
                };
            }).ToList();

            var total = new CarbonFinancialReport(all, today);
            double SchemeKwh(string schemeId) =>
                new CarbonFinancialReport(all.Where(s => s.SchemeId == schemeId), today).TotalKwhSaved;

            return new PeriodAnalytics(points, total.TotalKwhSaved, total.TotalCo2Reduced, total.TotalRupiahSaved,
                SchemeKwh(PowerSessionRecorder.PowerSaverSchemeId), SchemeKwh(PowerSessionRecorder.BalancedSchemeId),
                all.Sum(session => Math.Max(0, session.DurationHours)));
        }
    }
}
