using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using EcoKoin.Models;
using EcoKoin.Services;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        // OWNER: pet agent. EcoPet (state, image, messages), level/XP, the two "Rekomendasi Hemat Energi"
        // cards and their "Terapkan" buttons.
        private sealed record DashboardPetData(
            PetState State, string ImageFile, int KoinToday, int TotalKoin, int SleepCount, int SleepMinutes, int PowerSaverMinutes);

        private void InitializeDashboardPet(DashboardViewModel dashboard, AuthUser user,
            SqliteAccountService accounts, IClock clock)
        {
            if (string.IsNullOrEmpty(user.UserId))
                return;
            string userId = user.UserId;

            int sequence = 0;
            DateOnly shownDate = DateOnly.FromDateTime(clock.Now);
            DashboardPetData? lastData = null;
            var dateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };

            // The sleep card follows the saved preference; its reason follows the sessions recorded today.
            void ShowSleepCard()
            {
                string reason = lastData is null ? "Memuat data sleep…" : DescribeSleepToday(lastData);
                string delay = WelcomePreferencesStore.Load().SleepDelay;
                if (WindowsSleepTimeout.TryParseMinutes(delay, out int minutes))
                    dashboard.ShowSleepRecommendation($"Sleep setelah {minutes} menit idle", reason, true);
                else if (string.Equals(delay?.Trim(), "Tidak pernah", StringComparison.OrdinalIgnoreCase))
                    dashboard.ShowSleepRecommendation("Sleep otomatis dimatikan di preferensi", reason, false);
                else
                    dashboard.ShowSleepRecommendation("Preferensi sleep belum bisa dibaca", reason, false);
            }

            void ShowLevel(int totalKoin)
            {
                int total = Math.Max(0, totalKoin);
                dashboard.XpTarget = EcoKoin.Models.User.XpPerLevel;
                dashboard.Level = total / EcoKoin.Models.User.XpPerLevel + 1;
                dashboard.Xp = total % EcoKoin.Models.User.XpPerLevel;
            }

            async Task ReloadAsync()
            {
                int mine = ++sequence;
                DateOnly today = DateOnly.FromDateTime(clock.Now);
                DateTime now = clock.Now;
                try
                {
                    var data = await Task.Run(() =>
                    {
                        var repository = accounts.GetRepository();
                        EcoPet pet = repository.GetOrCreatePet(userId, today);
                        int total = repository.GetTotalEcoKoin(userId);
                        int koinToday = repository.GetKoinEarnedOn(userId, today);
                        DateTime from = today.ToDateTime(TimeOnly.MinValue);
                        var sessions = repository.GetPowerSessions(userId, from, from.AddDays(1));

                        int sleepCount = 0, sleepMinutes = 0, powerSaverMinutes = 0;
                        foreach (PowerSession session in sessions)
                        {
                            int minutes = (int)Math.Max(0, ((session.EndTime ?? now) - session.StartTime).TotalMinutes);
                            if (session.SchemeId == PowerSessionRecorder.BalancedSchemeId)
                            {
                                sleepCount++;
                                sleepMinutes += minutes;
                            }
                            else if (session.SchemeId == PowerSessionRecorder.PowerSaverSchemeId)
                            {
                                powerSaverMinutes += minutes;
                            }
                        }

                        return new DashboardPetData(pet.CurrentState, pet.Animate(), koinToday, total,
                            sleepCount, sleepMinutes, powerSaverMinutes);
                    });

                    if (mine != sequence)
                        return;
                    lastData = data;
                    shownDate = today;
                    ShowLevel(data.TotalKoin);
                    dashboard.ShowPet(data.State, data.ImageFile, data.KoinToday);
                    ShowSleepCard();
                }
                catch (Exception)
                {
                    // Database unavailable: keep what is shown; only say so if nothing was ever loaded.
                    if (mine == sequence && lastData is null)
                    {
                        dashboard.ShowPetUnavailable();
                        ShowSleepCard();
                    }
                }
            }

            // Immediate values so no placeholder literal flashes before the first database read.
            if (user.TotalEcoKoin is int knownTotal)
                ShowLevel(knownTotal);
            ShowSleepCard();

            EventHandler onDataChanged = async (_, _) => await ReloadAsync();
            Action onPreferencesChanged = ShowSleepCard;
            dateTimer.Tick += async (_, _) =>
            {
                if (DateOnly.FromDateTime(clock.Now) != shownDate)
                    await ReloadAsync();
            };

            Loaded += async (_, _) =>
            {
                dateTimer.Start();
                await ReloadAsync();
            };
            dashboard.DataChanged += onDataChanged;
            WelcomePreferencesStore.PreferencesChanged += onPreferencesChanged;
            Closed += (_, _) =>
            {
                dateTimer.Stop();
                dashboard.DataChanged -= onDataChanged;
                WelcomePreferencesStore.PreferencesChanged -= onPreferencesChanged;
            };
        }

        private static string DescribeSleepToday(DashboardPetData data)
        {
            var parts = new List<string>
            {
                data.SleepCount > 0
                    ? $"PC masuk sleep {data.SleepCount} kali hari ini ({data.SleepMinutes} menit)"
                    : "Belum ada data sleep hari ini"
            };
            if (data.PowerSaverMinutes > 0)
                parts.Add($"Power Saver aktif {data.PowerSaverMinutes} menit");
            return string.Join(". ", parts) + ".";
        }
    }
}
