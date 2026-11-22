using System.Diagnostics;
using EcoKoin.Services;
using Microsoft.Win32;

namespace EcoKoin.WPF.Models
{
    /// <summary>
    /// Pairs Suspend/Resume system power events into one sleep interval. SystemEvents raises on its own thread,
    /// so <paramref name="onResumed"/> is always started on the thread pool (never on that thread, never on the UI
    /// thread); the callee must marshal to the UI itself. Must be disposed to unsubscribe from the static event.
    /// </summary>
    internal sealed class PowerEventWatcher : IDisposable
    {
        private readonly IClock _clock;
        private readonly Func<TimeSpan> _idleTime;
        private readonly Func<DateTime, DateTime, TimeSpan, Task> _onResumed;
        private readonly object _gate = new();
        private DateTime? _suspendedAt;
        private DateTime? _lastResumeAt;
        private TimeSpan _idleAtSuspend;
        private bool _subscribed;
        private bool _disposed;

        /// <param name="onResumed">(suspendedAt, resumedAt, idleTimeWhenSuspended); runs on a thread-pool thread.</param>
        public PowerEventWatcher(IClock clock, Func<TimeSpan> idleTime, Func<DateTime, DateTime, TimeSpan, Task> onResumed)
        {
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(idleTime);
            ArgumentNullException.ThrowIfNull(onResumed);
            _clock = clock;
            _idleTime = idleTime;
            _onResumed = onResumed;
        }

        /// <summary>When the last paired Suspend/Resume finished; lets callers tell "Windows told us" from "we only noticed a gap".</summary>
        public DateTime? LastResumeAt
        {
            get { lock (_gate) return _lastResumeAt; }
        }

        /// <summary>Subscribes to SystemEvents.PowerModeChanged; calling it again is a no-op.</summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_subscribed || _disposed)
                    return;
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                _subscribed = true;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                if (_subscribed)
                {
                    SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                    _subscribed = false;
                }
                _suspendedAt = null;
            }
        }

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    HandleSuspend();
                    break;
                case PowerModes.Resume:
                    HandleResume();
                    break;
            }
        }

        // Internal so the pairing logic can be exercised without raising real system events.
        internal void HandleSuspend()
        {
            try
            {
                lock (_gate)
                {
                    if (_disposed || _suspendedAt is not null)
                        return; // repeated Suspend without Resume: keep the earliest one

                    TimeSpan idle;
                    try { idle = _idleTime(); }
                    catch (Exception ex) { Trace.TraceWarning($"PowerEventWatcher idle read failed: {ex}"); idle = TimeSpan.Zero; }

                    _idleAtSuspend = idle;
                    _suspendedAt = _clock.Now;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PowerEventWatcher suspend failed: {ex}");
            }
        }

        internal void HandleResume()
        {
            DateTime suspendedAt;
            TimeSpan idle;
            try
            {
                lock (_gate)
                {
                    if (_disposed || _suspendedAt is not { } started)
                        return; // Resume without Suspend: ignore

                    suspendedAt = started;
                    idle = _idleAtSuspend;
                    _suspendedAt = null;
                }

                DateTime resumedAt = _clock.Now;
                lock (_gate)
                    _lastResumeAt = resumedAt;
                _ = Task.Run(async () =>
                {
                    try { await _onResumed(suspendedAt, resumedAt, idle).ConfigureAwait(false); }
                    catch (Exception ex) { Trace.TraceWarning($"PowerEventWatcher resume handler failed: {ex}"); }
                });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"PowerEventWatcher resume failed: {ex}");
            }
        }
    }
}
