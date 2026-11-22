using System.Windows.Threading;
using EcoKoin.Models;
using EcoKoin.Services;
using EcoKoin.ViewModels;
using EcoKoin.WPF.Models;
using EcoKoin.WPF.ViewModels;

namespace EcoKoin.WPF
{
    public partial class MainWindow
    {
        // Daily quest list (dashboard card + Misi & Reward page): loads today's quests from the database,
        // claims Sleep / Weather quests and records quest events. The Power Saver tracker lives in
        // MainWindow.PowerSaverQuest.cs and feeds the same summary through DashboardViewModel.
        private DashboardViewModel? _questDashboard;
        private AuthUser? _questUser;
        private SqliteAccountService? _questAccounts;
        private IClock? _questClock;
        private int _questLoadVersion;
        private bool _questClosed;
        private bool _questSelfNotify;
        private DateOnly _questShownDate;
        private bool _questLoadFailed; // retried by the one-minute timer instead of staying broken until midnight

        private void InitializeDashboardQuests(DashboardViewModel dashboard, AuthUser user,
            SqliteAccountService accounts, IClock clock)
        {
            _questDashboard = dashboard;
            _questUser = user;
            _questAccounts = accounts;
            _questClock = clock;
            _questShownDate = DateOnly.FromDateTime(clock.Now);

            dashboard.SleepQuestItem.ClaimCommand = new RelayCommand(async _ => await ClaimQuestAsync(dashboard.SleepQuestItem));
            dashboard.WeatherQuestItem.ClaimCommand = new RelayCommand(async _ => await ClaimQuestAsync(dashboard.WeatherQuestItem));

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            timer.Tick += async (_, _) =>
            {
                if (_questLoadFailed || DateOnly.FromDateTime(clock.Now) != _questShownDate)
                    await ReloadQuestsAsync();
            };

            // DataChanged is raised by other features (and by this one after its own writes, which are
            // already applied, hence the _questSelfNotify guard against reload loops).
            EventHandler onDataChanged = async (_, _) =>
            {
                if (!_questSelfNotify)
                    await ReloadQuestsAsync();
            };
            dashboard.DataChanged += onDataChanged;

            Loaded += async (_, _) =>
            {
                timer.Start();
                await ReloadQuestsAsync();
            };
            Closed += (_, _) =>
            {
                _questClosed = true;
                timer.Stop();
                dashboard.DataChanged -= onDataChanged;
            };
        }

        private (IReadOnlyList<DailyQuest> Quests, int Balance) LoadQuestsFromDatabase(string userId, DateOnly date)
        {
            var repository = _questAccounts!.GetRepository();
            var quests = repository.EnsureDailyQuests(userId, date);
            return (quests, repository.GetTotalEcoKoin(userId));
        }

        private void ApplyQuests(IReadOnlyList<DailyQuest> quests, int balance, DateOnly date)
        {
            _questShownDate = date;
            _questLoadFailed = false;
            _questUser!.TotalEcoKoin = balance;
            _questDashboard!.ShowQuests(quests, balance);
        }

        private void RaiseQuestDataChanged()
        {
            _questSelfNotify = true;
            try
            {
                _questDashboard!.NotifyDataChanged();
            }
            finally
            {
                _questSelfNotify = false;
            }
        }

        // Reloads today's quests; a result that is overtaken by a newer reload is dropped.
        private async Task ReloadQuestsAsync()
        {
            if (_questDashboard is null || _questUser?.UserId is not { } userId || _questClock is null || _questClosed)
                return;

            int version = ++_questLoadVersion;
            var date = DateOnly.FromDateTime(_questClock.Now);
            try
            {
                var result = await Task.Run(() => LoadQuestsFromDatabase(userId, date));
                if (_questClosed || version != _questLoadVersion)
                    return;

                ApplyQuests(result.Quests, result.Balance, date);
            }
            catch (Exception ex)
            {
                if (!_questClosed && version == _questLoadVersion)
                {
                    _questLoadFailed = true;
                    _questDashboard.ShowQuestsUnavailable();
                }
                System.Diagnostics.Trace.TraceError("EcoKoin daily quests failed to load: {0}", ex.GetType().Name);
            }
        }

        private async Task ClaimQuestAsync(QuestItemViewModel item)
        {
            if (_questDashboard is null || _questUser?.UserId is not { } userId || _questClosed ||
                item.QuestId is not { } questId || !item.CanClaim)
                return;

            var questDate = item.QuestDate;
            item.CanClaim = false; // blocks a second click while the claim is running
            try
            {
                int version = ++_questLoadVersion;
                var result = await Task.Run(() =>
                {
                    var repository = _questAccounts!.GetRepository();
                    bool claimed = repository.TryClaimQuest(userId, questId, questDate);
                    var (quests, balance) = LoadQuestsFromDatabase(userId, DateOnly.FromDateTime(_questClock!.Now));
                    return (Claimed: claimed, Quests: quests, Balance: balance);
                });
                if (_questClosed)
                    return;

                if (version == _questLoadVersion)
                    ApplyQuests(result.Quests, result.Balance, DateOnly.FromDateTime(_questClock!.Now));
                else
                    await ReloadQuestsAsync(); // another write overtook this one: show the newest state

                if (result.Claimed)
                    RaiseQuestDataChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("EcoKoin quest claim failed: {0}", ex.GetType().Name);
                await ReloadQuestsAsync();
            }
        }

        // Other features call this when something happened that a quest may count (a weather recommendation
        // was applied, the PC went to sleep...). Safe to call from the UI thread; persists the progress, reloads
        // the quest list and calls dashboard.NotifyDataChanged(). No-op when nobody is logged in, the quest is
        // missing or already claimed, or the event kind is not tracked here (Power Saver minutes are owned by
        // the Power Saver tracker).
        private async Task RecordQuestEventAsync(QuestEventKind kind, int amount)
        {
            QuestType? type = kind switch
            {
                QuestEventKind.SleepEntered => QuestType.Sleep,
                QuestEventKind.WeatherRecommendationApplied => QuestType.WeatherAdaptive,
                _ => null
            };
            if (type is not { } questType || amount <= 0 ||
                _questDashboard is null || _questUser?.UserId is not { } userId || _questClock is null || _questClosed)
                return;

            var date = DateOnly.FromDateTime(_questClock.Now);
            try
            {
                int version = ++_questLoadVersion;
                var result = await Task.Run(() =>
                {
                    var repository = _questAccounts!.GetRepository();
                    repository.EnsureDailyQuests(userId, date);
                    int progress = repository.AddQuestProgress(userId, questType, date, amount);
                    var (quests, balance) = LoadQuestsFromDatabase(userId, date);
                    return (Progress: progress, Quests: quests, Balance: balance);
                });
                if (_questClosed)
                    return;

                if (version == _questLoadVersion)
                    ApplyQuests(result.Quests, result.Balance, date);
                else
                    await ReloadQuestsAsync(); // another write overtook this one: show the newest state

                if (result.Progress >= 0)
                    RaiseQuestDataChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("EcoKoin quest event failed: {0}", ex.GetType().Name);
            }
        }
    }
}
