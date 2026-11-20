using System.Windows.Input;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.ViewModels
{
    /// <summary>
    /// Dashboard ViewModel. Spec: docs/03-domain-model.md (MainViewModel). Bound by the WPF view.
    /// Contains no timers: the UI layer (e.g. a DispatcherTimer) calls <see cref="OnTick"/> periodically
    /// (suggested: every minute) so the logic stays deterministic and testable.
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        /// <summary>Schemes with CpuMaxPerformance at or below this count as "Power Saver" for the PowerSaver quest.</summary>
        public const int PowerSaverMaxCpu = 50;

        private readonly SqliteRepository _repo;
        private readonly IClock _clock;

        private readonly RelayCommand _applyRecommendationCommand;
        private readonly RelayCommand _claimQuestCommand;
        private readonly RelayCommand _refreshCommand;

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

        private DateOnly _questDate;
        private TimeSpan _powerSaverRemainder = TimeSpan.Zero;

        public MainViewModel(SqliteRepository repo, PowerMonitorService power, WeatherService weather, IClock clock)
        {
            _repo = repo;
            _clock = clock;
            PowerService = power;
            WeatherService = weather;

            _applyRecommendationCommand = new RelayCommand(_ => ApplyRecommendation(), _ => CurrentUser is not null && CurrentWeather is not null);
            _claimQuestCommand = new RelayCommand(p => ClaimQuest((DailyQuest)p!), p => p is DailyQuest q && !q.IsClaimed && q.CheckCompletion());
            _refreshCommand = new RelayCommand(_ => RefreshDashboard(), _ => CurrentUser is not null);

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

        public ICommand ApplyRecommendationCommand { get; protected set; }
        /// <summary>Parameter: the DailyQuest to claim.</summary>
        public ICommand ClaimQuestCommand { get; protected set; }
        public ICommand RefreshCommand { get; protected set; }

        /// <summary>
        /// Loads user (by username), today's quests, pet and schemes, then refreshes the dashboard.
        /// Throws <see cref="InvalidOperationException"/> if the user does not exist.
        /// </summary>
        public void Initialize(string username)
        {
            _repo.InitializeDatabase();

            var user = _repo.GetUserByUsername(username)
                ?? throw new InvalidOperationException($"User '{username}' does not exist.");

            CurrentUser = user;
            LoadQuests();

            Schemes = _repo.GetPowerSchemes();
            ActiveScheme = Schemes.FirstOrDefault(s => s.SchemeId == "scheme-balanced")
                        ?? Schemes.FirstOrDefault(s => s.SchemeName.Equals("Balanced", StringComparison.OrdinalIgnoreCase));

            RefreshDashboard();
        }

        /// <summary>Updates weather, power state, quests and weekly report. Blocks on the weather request; see <see cref="RefreshDashboardAsync"/>.</summary>
        public void RefreshDashboard()
        {
            var user = RequireUser();

            WeatherData? weather = null;
            bool failed = false;
            try { weather = WeatherService.FetchCurrentWeather(); }
            catch (WeatherUnavailableException) { failed = true; }

            RefreshCore(user, weather, failed);
        }

        /// <summary>Same as <see cref="RefreshDashboard"/> but does not block the caller while fetching weather.</summary>
        public async Task RefreshDashboardAsync(CancellationToken ct = default)
        {
            var user = RequireUser();

            WeatherData? weather = null;
            bool failed = false;
            try { weather = await WeatherService.FetchCurrentWeatherAsync(ct); }
            catch (WeatherUnavailableException) { failed = true; }

            RefreshCore(user, weather, failed);
        }

        /// <summary>
        /// Called by the UI timer. Accrues PowerSaver quest minutes while a power-saver scheme is active
        /// (whole minutes only), then refreshes the dashboard.
        /// </summary>
        public void OnTick(TimeSpan elapsed)
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
                    RecordEvent(new QuestEvent(QuestEventKind.PowerSaverMinutes, minutes));
                }
            }

            RefreshDashboard();
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
        public void RecordEvent(QuestEvent evt)
        {
            RequireUser();

            bool changed = false;
            foreach (var quest in Quests.Where(q => !q.IsClaimed))
            {
                int amount = quest.Evaluate(evt);
                if (amount <= 0 || quest.CheckCompletion())
                    continue;

                quest.UpdateProgress(amount);
                _repo.SaveQuestProgress(quest);
                changed = true;
            }

            if (changed)
                PublishQuests();
        }

        private void ApplyRecommendation()
        {
            RecordEvent(new QuestEvent(QuestEventKind.WeatherRecommendationApplied));
        }

        private void ClaimQuest(DailyQuest quest)
        {
            var user = RequireUser();

            if (!quest.Claim(user))
                return;

            user.Pet ??= new EcoPet(Guid.NewGuid().ToString(), user.UserId);
            user.Pet.UpdateState(CoinsEarnedToday());

            _repo.SaveQuestProgress(quest);
            _repo.UpdateUserProfile(user);

            OnPropertyChanged(nameof(CurrentUser));
            PublishQuests();
        }

        private void LoadQuests()
        {
            var user = RequireUser();
            var today = DateOnly.FromDateTime(_clock.Now);

            var todays = _repo.EnsureDailyQuests(user.UserId, today);
            user.Quests.Clear();
            user.Quests.AddRange(todays);
            _questDate = today;

            user.Pet ??= new EcoPet(Guid.NewGuid().ToString(), user.UserId);
            user.Pet.UpdateState(CoinsEarnedToday());
            _repo.UpdateUserProfile(user);

            PublishQuests();
        }

        private void RefreshCore(User user, WeatherData? weather, bool weatherFailed)
        {
            if (DateOnly.FromDateTime(_clock.Now) != _questDate)
                LoadQuests(); // new day: fresh quests, pet recomputed

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
            WeeklyReport = new CarbonFinancialReport(_repo.GetWeeklyReportData(user.UserId, now), DateOnly.FromDateTime(now));

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
            CurrentUser ?? throw new InvalidOperationException("Call Initialize(username) first.");

        private void RaiseCommandStates()
        {
            _applyRecommendationCommand.RaiseCanExecuteChanged();
            _claimQuestCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
        }
    }
}
