using System.Windows.Threading;
using EcoKoin.Services;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        // OWNER: analytics agent. Fills the dashboard "ANALISIS MINGGUAN" card (energy, CO2, rupiah,
        // weekly koin, 7-day chart) from POWER_SESSIONS / claimed quests.
        private void InitializeDashboardAnalytics(DashboardViewModel dashboard, AuthUser user,
            AccountService accounts, IClock clock)
        {
            string userId = user.UserId!;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            int latestRequest = 0;
            bool closed = false;

            async Task RefreshAsync()
            {
                if (closed)
                    return;

                int request = ++latestRequest;
                var today = DateOnly.FromDateTime(clock.Now);
                try
                {
                    var week = await AnalyticsLoader.LoadWeekAsync(accounts.GetRepository(), userId, today);
                    // A newer refresh (data changed meanwhile) supersedes this result.
                    if (!closed && request == latestRequest)
                        dashboard.ShowWeeklyAnalytics(week);
                }
                catch (Exception ex)
                {
                    if (!closed && request == latestRequest)
                        dashboard.ShowWeeklyAnalyticsError();
                    System.Diagnostics.Trace.TraceError("EcoKoin weekly analytics failed: {0}", ex.GetType().Name);
                }
            }

            void OnDataChanged(object? sender, EventArgs e) => _ = RefreshAsync();

            dashboard.DataChanged += OnDataChanged;
            // The tick also picks up a changed calendar date, because the window is recomputed from the clock.
            timer.Tick += (_, _) => _ = RefreshAsync();
            Loaded += (_, _) =>
            {
                timer.Start();
                _ = RefreshAsync();
            };
            Closed += (_, _) =>
            {
                closed = true;
                timer.Stop();
                dashboard.DataChanged -= OnDataChanged;
            };
        }
    }
}
