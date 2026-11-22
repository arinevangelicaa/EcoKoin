using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using EcoKoin.Models;
using EcoKoin.ViewModels;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        private enum QuestClaimTarget
        {
            None,
            PowerSaver,
            Weather
        }

        private Func<Task>? _refreshQuestsAction;

        internal Task RefreshActiveQuestsAsync() =>
            _refreshQuestsAction is not null ? _refreshQuestsAction() : Task.CompletedTask;

        private void InitializePowerSaverQuest(DashboardViewModel dashboard, AuthUser user, SqliteAccountService accounts,
            WindowsPowerSaverStatus powerStatus)
        {
            TrackPowerSaverQuest(dashboard, user, accounts, powerStatus.IsActive, () => DateTimeOffset.Now,
                new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) });
        }

        private void TrackPowerSaverQuest(DashboardViewModel dashboard, AuthUser user, SqliteAccountService accounts,
            Func<bool?> isPowerSaverActive, Func<DateTimeOffset> getTime, DispatcherTimer timer)
        {
            string userId = user.UserId!;
            DateTimeOffset? previousActiveSample = null;
            PowerSaverQuest? displayedPowerQuest = null;
            WeatherAdaptiveQuest? displayedWeatherQuest = null;
            bool busy = false;
            bool closed = false;

            async Task RefreshAsync(QuestClaimTarget claimTarget = QuestClaimTarget.None)
            {
                if (busy || closed)
                    return;

                busy = true;
                dashboard.CanClaimPowerSaver = false;
                dashboard.CanClaimWeatherQuest = false;
                try
                {
                    var now = getTime();
                    var date = DateOnly.FromDateTime(now.LocalDateTime);
                    var previous = previousActiveSample;
                    var result = await Task.Run(() =>
                    {
                        var repository = accounts.GetRepository();
                        var allQuests = repository.EnsureDailyQuests(userId, date);
                        var power = allQuests.OfType<PowerSaverQuest>().First();
                        var weather = allQuests.OfType<WeatherAdaptiveQuest>().First();

                        if (claimTarget == QuestClaimTarget.PowerSaver)
                        {
                            repository.TryClaimPowerSaverQuest(userId, power.QuestId, date);
                            allQuests = repository.EnsureDailyQuests(userId, date);
                            power = allQuests.OfType<PowerSaverQuest>().First();
                        }
                        else if (claimTarget == QuestClaimTarget.Weather)
                        {
                            repository.TryClaimWeatherQuest(userId, date);
                            allQuests = repository.EnsureDailyQuests(userId, date);
                            weather = allQuests.OfType<WeatherAdaptiveQuest>().First();
                        }

                        bool? active = claimTarget == QuestClaimTarget.None ? isPowerSaverActive() : null;
                        if (active == true && previous is { } last &&
                            DateOnly.FromDateTime(last.LocalDateTime) == date &&
                            now - last >= TimeSpan.FromSeconds(55) &&
                            now - last <= TimeSpan.FromSeconds(75) &&
                            now.ToUnixTimeSeconds() / 60 == last.ToUnixTimeSeconds() / 60 + 1)
                        {
                            repository.RecordPowerSaverMinute(userId, power.QuestId, date, now.ToUnixTimeSeconds() / 60);
                            allQuests = repository.EnsureDailyQuests(userId, date);
                            power = allQuests.OfType<PowerSaverQuest>().First();
                        }

                        int coinsToday = repository.GetCoinsEarnedToday(userId, date);
                        var userRow = repository.GetUserByUsername(user.Email);
                        long weeklyMinutes = repository.GetPowerSaverMinutes(userId, date.AddDays(-6), date);
                        var dailyMinutes = repository.GetDailyPowerSaverMinutes(userId, date.AddDays(-6), date);

                        return (Power: power, Weather: weather, Active: active,
                            Balance: userRow?.TotalEcoKoin,
                            CoinsToday: coinsToday,
                            WeeklyMinutes: weeklyMinutes,
                            DailyMinutes: dailyMinutes);
                    });

                    if (closed)
                        return;

                    if (claimTarget == QuestClaimTarget.None)
                        previousActiveSample = result.Active == true ? now : null;

                    displayedPowerQuest = result.Power;
                    displayedWeatherQuest = result.Weather;
                    user.TotalEcoKoin = result.Balance;

                    int balance = result.Balance ?? 0;
                    dashboard.UpdateAccountStats(balance, result.CoinsToday);
                    dashboard.ShowPowerSaverQuest(result.Power, result.Balance, result.Active);
                    dashboard.ShowWeatherQuest(result.Weather);

                    // Update Analysis KPI and weekly 7-day chart
                    double weeklyKwh = (20.0 * (result.WeeklyMinutes / 60.0)) / 1000.0;
                    double weeklyCo2 = weeklyKwh * 0.87;
                    double weeklyRp = weeklyKwh * 1444.70;

                    var idCulture = CultureInfo.GetCultureInfo("id-ID");
                    var points = new List<AnalysisDataPoint>(7);
                    var chartValues = new double[7];
                    var todayDate = DateOnly.FromDateTime(now.LocalDateTime);
                    for (int i = 6; i >= 0; i--)
                    {
                        var d = todayDate.AddDays(-i);
                        long m = result.DailyMinutes.TryGetValue(d, out var min) ? min : 0;
                        double dayKwh = (20.0 * (m / 60.0)) / 1000.0;
                        points.Add(new AnalysisDataPoint
                        {
                            Label = d.ToString("ddd", idCulture),
                            Co2SavedKg = dayKwh * 0.87,
                            MoneySavedRp = dayKwh * 1444.70
                        });
                        chartValues[6 - i] = dayKwh;
                    }

                    AnalysisContent.UpdatePeriodData(0, points, weeklyKwh, weeklyCo2, weeklyRp);
                    AnalysisContent.ShowWeeklyPowerSaverMinutes(result.WeeklyMinutes);
                    dashboard.SetGrafikMingguan(chartValues, isDemo: false);
                }
                catch (Exception ex)
                {
                    previousActiveSample = null;
                    if (!closed)
                        dashboard.PowerSaverStatus = "Misi belum dapat dimuat. EcoKoin akan mencoba lagi.";
                    System.Diagnostics.Trace.TraceError("EcoKoin Quest tracking failed: {0}", ex.GetType().Name);
                }
                finally
                {
                    busy = false;
                    if (!closed)
                    {
                        if (displayedPowerQuest is { IsClaimed: false } pq)
                            dashboard.CanClaimPowerSaver = pq.CheckCompletion();
                        if (displayedWeatherQuest is { IsClaimed: false } wq)
                            dashboard.CanClaimWeatherQuest = wq.CheckCompletion();
                    }
                }
            }

            _refreshQuestsAction = () => RefreshAsync(QuestClaimTarget.None);

            dashboard.ClaimPowerSaverCommand = new RelayCommand(async _ =>
            {
                if (displayedPowerQuest is not null)
                    await RefreshAsync(QuestClaimTarget.PowerSaver);
            });

            dashboard.ClaimWeatherQuestCommand = new RelayCommand(async _ =>
            {
                if (displayedWeatherQuest is not null)
                    await RefreshAsync(QuestClaimTarget.Weather);
            });

            timer.Tick += async (_, _) => await RefreshAsync(QuestClaimTarget.None);
            Loaded += async (_, _) =>
            {
                timer.Start();
                await RefreshAsync(QuestClaimTarget.None);
            };
            Closed += (_, _) =>
            {
                closed = true;
                timer.Stop();
            };
        }
    }
}
