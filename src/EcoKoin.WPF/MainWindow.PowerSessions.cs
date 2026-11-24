using System.Diagnostics;
using System.Windows.Threading;
using EcoKoin.Models;
using EcoKoin.Services;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        private static readonly TimeSpan PowerSessionTickInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan PowerSessionMinSleep = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan PowerSessionGapSleep = TimeSpan.FromMinutes(3);

        // Records POWER_SESSIONS (Power Saver active periods, sleep periods) that the analytics read, and calls
        // dashboard.NotifyDataChanged() on the UI thread when one was written.
        private void InitializePowerSessions(DashboardViewModel dashboard, AuthUser user,
            AccountService accounts, WindowsPowerSaverStatus powerStatus, IClock clock)
        {
            string? userId = user.UserId;
            if (userId is null)
                return;

            var recorder = new PowerSessionRecorder(accounts.GetRepository(), clock);
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PowerSessionTickInterval };
            var idleProvider = new WindowsIdleTimeProvider();
            bool closed = false;
            int tickRunning = 0;
            PowerEventWatcher? watcher = null;
            DateTime? lastTickAt = null;

            async Task TickAsync()
            {
                // Skip while the previous tick is still running (powercfg can be slow).
                if (closed || Interlocked.Exchange(ref tickRunning, 1) == 1)
                    return;
                try
                {
                    DateTime tickAt = clock.Now;
                    DateTime? previousTick = lastTickAt;
                    lastTickAt = tickAt;

                    // A long hole between two one-minute ticks means the PC was asleep. Normally Windows raises
                    // Suspend/Resume (handled by the watcher below); on Modern Standby laptops it often does not,
                    // so when no resume event covers the hole, the hole itself is recorded as the sleep period.
                    bool missedSleep = previousTick is { } previous
                        && tickAt - previous >= PowerSessionGapSleep
                        && !(watcher?.LastResumeAt is { } resumed && resumed >= previous);

                    var outcome = await Task.Run(async () =>
                    {
                        bool? active = powerStatus.IsActive();
                        bool wrote = await recorder.RecordPowerSaverTickAsync(userId, active);
                        bool sleepSaved = missedSleep && await recorder.RecordSleepAsync(userId, previousTick!.Value, tickAt);
                        return (Wrote: wrote, SleepSaved: sleepSaved);
                    });
                    if (closed)
                        return;

                    // Back on the UI thread (captured context).
                    if (outcome.Wrote || outcome.SleepSaved)
                        dashboard.NotifyDataChanged();
                    if (outcome.SleepSaved)
                        await RecordQuestEventAsync(QuestEventKind.SleepEntered, 1);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Power Saver session tick failed: {ex}");
                }
                finally
                {
                    Interlocked.Exchange(ref tickRunning, 0);
                }
            }

            watcher = new PowerEventWatcher(clock, idleProvider.GetIdleTime, async (suspendedAt, resumedAt, idle) =>
            {
                // Thread-pool thread: DB write here, UI work marshalled below.
                bool saved = await recorder.RecordSleepAsync(userId, suspendedAt, resumedAt);
                if (!saved && resumedAt - suspendedAt < PowerSessionMinSleep)
                    return;

                bool wasAway = idle >= PowerSessionMinSleep;
                await await Dispatcher.InvokeAsync(async () =>
                {
                    if (closed)
                        return;
                    dashboard.NotifyDataChanged();
                    if (wasAway)
                        await RecordQuestEventAsync(QuestEventKind.SleepEntered, 1);
                });
            });

            bool started = false;
            timer.Tick += async (_, _) => await TickAsync();
            Loaded += async (_, _) =>
            {
                if (started || closed)
                    return; // Loaded can fire again if the window is re-parented
                started = true;
                watcher!.Start();
                timer.Start();
                await TickAsync();
            };
            Closed += (_, _) =>
            {
                closed = true;
                timer.Stop();
                watcher?.Dispose(); // unsubscribes from the static SystemEvents.PowerModeChanged
            };
        }
    }
}
