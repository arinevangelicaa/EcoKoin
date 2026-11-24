using System.Windows.Threading;
using EcoKoin.Models;
using EcoKoin.ViewModels;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        private void InitializePowerSaverQuest(DashboardViewModel dashboard, AuthUser user, AccountService accounts,
            WindowsPowerSaverStatus powerStatus)
        {
            TrackPowerSaverQuest(dashboard, user, accounts, powerStatus.IsActive, () => DateTimeOffset.Now,
                new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) });
        }

        private void TrackPowerSaverQuest(DashboardViewModel dashboard, AuthUser user, AccountService accounts,
            Func<bool?> isPowerSaverActive, Func<DateTimeOffset> getTime, DispatcherTimer timer)
        {
            string userId = user.UserId!;
            DateTimeOffset? previousActiveSample = null;
            PowerSaverQuest? displayedQuest = null;
            bool busy = false;
            bool closed = false;

            async Task RefreshAsync(PowerSaverQuest? claim = null)
            {
                if (busy || closed)
                    return;

                busy = true;
                dashboard.CanClaimPowerSaver = false;
                try
                {
                    var now = getTime();
                    var date = DateOnly.FromDateTime(now.LocalDateTime);
                    var previous = previousActiveSample;
                    var result = await Task.Run(async () =>
                    {
                        var repository = accounts.GetRepository();
                        var quest = (await repository.EnsureDailyQuestsAsync(userId, date)).OfType<PowerSaverQuest>().First();
                        if (claim is not null)
                        {
                            await repository.TryClaimPowerSaverQuestAsync(userId, claim.QuestId, date);
                            return (Quest: (await repository.EnsureDailyQuestsAsync(userId, date)).OfType<PowerSaverQuest>().First(),
                                Active: (bool?)null, Balance: (await repository.GetUserByUsernameAsync(user.Email))?.TotalEcoKoin);
                        }

                        bool? active = isPowerSaverActive();
                        if (active == true && previous is { } last &&
                            DateOnly.FromDateTime(last.LocalDateTime) == date &&
                            now - last >= TimeSpan.FromSeconds(55) &&
                            now - last <= TimeSpan.FromSeconds(75) &&
                            now.ToUnixTimeSeconds() / 60 == last.ToUnixTimeSeconds() / 60 + 1)
                        {
                            await repository.RecordPowerSaverMinuteAsync(userId, quest.QuestId, date, now.ToUnixTimeSeconds() / 60);
                            quest = (await repository.EnsureDailyQuestsAsync(userId, date)).OfType<PowerSaverQuest>().First();
                        }

                        return (Quest: quest, Active: active, Balance: (await repository.GetUserByUsernameAsync(user.Email))?.TotalEcoKoin);
                    });
                    if (closed)
                        return;

                    if (claim is null)
                        previousActiveSample = result.Active == true ? now : null;
                    displayedQuest = result.Quest;
                    user.TotalEcoKoin = result.Balance;
                    dashboard.ShowPowerSaverQuest(result.Quest, result.Balance, result.Active);
                }
                catch (Exception ex)
                {
                    previousActiveSample = null;
                    if (!closed)
                        dashboard.PowerSaverStatus = "Misi belum dapat dimuat. EcoKoin akan mencoba lagi.";
                    System.Diagnostics.Trace.TraceError("EcoKoin Power Saver quest failed: {0}", ex.GetType().Name);
                }
                finally
                {
                    busy = false;
                    if (!closed && displayedQuest is { IsClaimed: false } quest)
                        dashboard.CanClaimPowerSaver = quest.CheckCompletion();
                }
            }

            dashboard.ClaimPowerSaverCommand = new RelayCommand(async _ =>
            {
                if (displayedQuest is not null)
                    await RefreshAsync(displayedQuest);
            });
            timer.Tick += async (_, _) => await RefreshAsync();
            Loaded += async (_, _) =>
            {
                timer.Start();
                await RefreshAsync();
            };
            Closed += (_, _) =>
            {
                closed = true;
                timer.Stop();
            };
        }
    }
}
