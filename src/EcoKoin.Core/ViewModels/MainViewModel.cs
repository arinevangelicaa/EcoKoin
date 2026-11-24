using System.Windows.Input;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.ViewModels
{
    /// <summary>
    /// Dashboard ViewModel. Spec: docs/03-domain-model.md (MainViewModel). Bound by the WPF view.
    /// Contains no timers: the UI layer (e.g. a DispatcherTimer) calls <see cref="OnTickAsync"/> periodically
    /// (suggested: every minute) so the logic stays deterministic and testable.
    /// Everything that touches the (remote) repository is asynchronous; call these methods from the UI thread
    /// and await them so property changes are raised on the UI thread.
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        /// <summary>Schemes with CpuMaxPerformance at or below this count as "Power Saver" for the PowerSaver quest.</summary>
        public const int PowerSaverMaxCpu = 50;

        private readonly IAppRepository _repo;
        private readonly IClock _clock;

        private readonly AsyncRelayCommand _applyRecommendationCommand;
        private readonly AsyncRelayCommand _claimQuestCommand;
        private readonly AsyncRelayCommand _refreshCommand;

        private User? _currentUser;
        private WeatherData? _currentWeather;
        private string _brightnessRecommendation = "";
        private PowerScheme? _activeScheme;
        private IReadOnlyList<DailyQuest> _quests = Array.Empty<DailyQuest>();
        private IReadOnlyList<PowerScheme> _schemes = Array.Empty<PowerScheme>();
        private CarbonFinancialReport? _weeklyReport;
        private bool _weatherStale;
        private bool _isIdle;
        private double _estimatedWattSaved;
        private Exception? _lastError;

        private DateOnly _questDate;
        private TimeSpan _powerSaverRemainder = TimeSpan.Zero;

        public MainViewModel(IAppRepository repo, PowerMonitorService power, WeatherService weather, IClock clock)
        {
            _repo = repo;
            _clock = clock;
            PowerService = power;
            WeatherService = weather;

            _applyRecommendationCommand = new AsyncRelayCommand(_ => RecordEventAsync(new QuestEvent(QuestEventKind.WeatherRecommendationApplied)),
                _ => CurrentUser is not null && CurrentWeather is not null, OnCommandFailed);
            _claimQuestCommand = new AsyncRelayCommand(p => ClaimQuestAsync((DailyQuest)p!),
                p => p is DailyQuest q && !q.IsClaimed && q.CheckCompletion(), OnCommandFailed);
            _refreshCommand = new AsyncRelayCommand(_ => RefreshDashboardAsync(), _ => CurrentUser is not null, OnCommandFailed);

            ApplyRecommendationCommand = _applyRecommendationCommand;
            ClaimQuestCommand = _claimQuestCommand;
            RefreshCommand = _refreshCommand;
        }

        public PowerMonitorService PowerService { get; }
        public WeatherService WeatherService { get; }

        public User? CurrentUser { get => _currentUser; set => SetProperty(ref _currentUser, value); }
        public WeatherData? CurrentWeather { get => _currentWeather; set => SetProperty(ref _currentWeather, value); }
        public string BrightnessRecommendation { get => _brightnessRecommendation; set => SetProperty(ref _brightnessRecommendation, value); }
        public PowerScheme? ActiveScheme { get => _activeScheme; set => SetProperty(ref _activeScheme, value); }
        public IReadOnlyList<DailyQuest> Quests { get => _quests; set => SetProperty(ref _quests, value); }
        public CarbonFinancialReport? WeeklyReport { get => _weeklyReport; set => SetProperty(ref _weeklyReport, value); }

        /// <summary>All power schemes from the database (lowest CPU limit first).</summary>
        public IReadOnlyList<PowerScheme> Schemes { get => _schemes; private set => SetProperty(ref _schemes, value); }

        /// <summary>True when the last weather fetch failed or returned data older than the cache lifetime.</summary>
        public bool WeatherStale { get => _weatherStale; private set => SetProperty(ref _weatherStale, value); }

        /// <summary>Result of the last idle check.</summary>
        public bool IsIdle { get => _isIdle; private set => SetProperty(ref _isIdle, value); }

        /// <summary>Estimated watts saved right now (estimate, see <see cref="PowerMonitorService.CalculateWattSaved"/>).</summary>
        public double EstimatedWattSaved { get => _estimatedWattSaved; private set => SetProperty(ref _estimatedWattSaved, value); }

        /// <summary>Last exception thrown by one of the commands (null if none). Bind it to show an error banner.</summary>
        public Exception? LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }

        /// <summary>Raised on the calling context whenever a command fails (database or network error, ...).</summary>
        public event EventHandler<Exception>? CommandFailed;

        public AsyncRelayCommand ApplyRecommendationCommand { get; protected set; }
        /// <summary>Parameter: the DailyQuest to claim.</summary>
        public AsyncRelayCommand ClaimQuestCommand { get; protected set; }
        public AsyncRelayCommand RefreshCommand { get; protected set; }

        /// <summary>
        /// Loads user (by username), today's quests, pet and schemes, then refreshes the dashboard.
        /// Throws <see cref="InvalidOperationException"/> if the user does not exist.
        /// </summary>
        public async Task InitializeAsync(string username, CancellationToken ct = default)
        {
            await _repo.InitializeDatabaseAsync(ct);

            var user = await _repo.GetUserByUsernameAsync(username, ct)
                ?? throw new InvalidOperationException($"User '{username}' does not exist.");

            CurrentUser = user;
            await LoadQuestsAsync(ct);

            Schemes = await _repo.GetPowerSchemesAsync(ct);
            ActiveScheme = Schemes.FirstOrDefault(s => s.SchemeId == "scheme-balanced")
                        ?? Schemes.FirstOrDefault(s => s.SchemeName.Equals("Balanced", StringComparison.OrdinalIgnoreCase));

            await RefreshDashboardAsync(ct);
        }

        /// <summary>
        /// Updates weather, power state, quests and weekly report. A failing weather request never fails the refresh
        /// (the last data is kept and <see cref="WeatherStale"/> is set); repository errors do propagate.
        /// </summary>
        public async Task RefreshDashboardAsync(CancellationToken ct = default)
        {
            var user = RequireUser();

            WeatherData? weather = null;
            bool failed = false;
            try { weather = await WeatherService.FetchCurrentWeatherAsync(ct); }
            catch (WeatherUnavailableException) { failed = true; }

            await RefreshCoreAsync(user, weather, failed, ct);
        }

        /// <summary>
        /// Called by the UI timer. Accrues PowerSaver quest minutes while a power-saver scheme is active
        /// (whole minutes only), then refreshes the dashboard.
        /// </summary>
        public async Task OnTickAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            if (CurrentUser is null)
                return;

            if (elapsed > TimeSpan.Zero && ActiveScheme is { CpuMaxPerformance: <= PowerSaverMaxCpu })
            {
                _powerSaverRemainder += elapsed;
                int minutes = (int)_powerSaverRemainder.TotalMinutes;
                if (minutes > 0)
                {
                    _powerSaverRemainder -= TimeSpan.FromMinutes(minutes);
                    await RecordEventAsync(new QuestEvent(QuestEventKind.PowerSaverMinutes, minutes), ct);
                }
            }

            await RefreshDashboardAsync(ct);
        }

        /// <summary>Activates <paramref name="scheme"/> through the power service and makes it the active scheme.</summary>
        public void ApplyScheme(PowerScheme scheme)
        {
            PowerService.ApplyPowerScheme(scheme);
            ActiveScheme = scheme;
        }

        /// <summary>
        /// Feeds an event to every unclaimed quest (<see cref="DailyQuest.Evaluate"/> then
        /// <see cref="DailyQuest.UpdateProgress"/>) and persists changed quests.
        /// The power/sleep layer calls this with <see cref="QuestEventKind.SleepEntered"/>.
        /// </summary>
        public async Task RecordEventAsync(QuestEvent evt, CancellationToken ct = default)
        {
            RequireUser();

            bool changed = false;
            foreach (var quest in Quests.Where(q => !q.IsClaimed))
            {
                int amount = quest.Evaluate(evt);
                if (amount <= 0 || quest.CheckCompletion())
                    continue;

                quest.UpdateProgress(amount);
                await _repo.SaveQuestProgressAsync(quest, ct);
                changed = true;
            }

            if (changed)
                PublishQuests();
        }

        private async Task ClaimQuestAsync(DailyQuest quest)
        {
            var user = RequireUser();

            if (!quest.Claim(user))
                return;

            user.Pet ??= new EcoPet(Guid.NewGuid().ToString(), user.UserId);
            user.Pet.UpdateState(CoinsEarnedToday());

            await _repo.SaveQuestProgressAsync(quest);
            await _repo.UpdateUserProfileAsync(user);

            OnPropertyChanged(nameof(CurrentUser));
            PublishQuests();
        }

        private async Task LoadQuestsAsync(CancellationToken ct = default)
        {
            var user = RequireUser();
            var today = DateOnly.FromDateTime(_clock.Now);

            var todays = await _repo.EnsureDailyQuestsAsync(user.UserId, today, ct);
            user.Quests.Clear();
            user.Quests.AddRange(todays);
            _questDate = today;

            user.Pet ??= new EcoPet(Guid.NewGuid().ToString(), user.UserId);
            user.Pet.UpdateState(CoinsEarnedToday());
            await _repo.UpdateUserProfileAsync(user, ct);

            PublishQuests();
        }

        private async Task RefreshCoreAsync(User user, WeatherData? weather, bool weatherFailed, CancellationToken ct)
        {
            if (DateOnly.FromDateTime(_clock.Now) != _questDate)
                await LoadQuestsAsync(ct); // new day: fresh quests, pet recomputed

            if (weather is not null)
            {
                CurrentWeather = weather;
                WeatherStale = _clock.Now - weather.Timestamp > TimeSpan.FromMinutes(WeatherService.CacheMinutes);
            }
            else if (weatherFailed)
            {
                WeatherStale = true;
            }

            BrightnessRecommendation = CurrentWeather is null ? "" : WeatherService.GetBrightnessRecommendation(CurrentWeather);

            IsIdle = PowerService.DetectIdleState();
            EstimatedWattSaved = PowerService.CalculateWattSaved();

            var now = _clock.Now;
            WeeklyReport = new CarbonFinancialReport(await _repo.GetWeeklyReportDataAsync(user.UserId, now, ct), DateOnly.FromDateTime(now));

            PublishQuests();
            RaiseCommandStates();
        }

        private void PublishQuests()
        {
            // A new list instance so bindings refresh (quest objects themselves are not observable).
            Quests = CurrentUser!.Quests.ToList();
            RaiseCommandStates();
        }

        private int CoinsEarnedToday() => CurrentUser!.Quests.Where(q => q.IsClaimed).Sum(q => q.RewardCoin);

        private User RequireUser() =>
            CurrentUser ?? throw new InvalidOperationException("Call InitializeAsync(username) first.");

        private void OnCommandFailed(Exception ex)
        {
            LastError = ex;
            CommandFailed?.Invoke(this, ex);
        }

        private void RaiseCommandStates()
        {
            _applyRecommendationCommand.RaiseCanExecuteChanged();
            _claimQuestCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
        }
    }
}
