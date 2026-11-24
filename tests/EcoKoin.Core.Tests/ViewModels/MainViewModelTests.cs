using System.Net;
using System.Net.Http;
using EcoKoin.Core.Tests.Services;
using EcoKoin.Core.Tests.Support;
using EcoKoin.Models;
using EcoKoin.Services;
using EcoKoin.ViewModels;

namespace EcoKoin.Core.Tests.ViewModels;

public class MainViewModelTests : IAsyncLifetime, IDisposable
{
    private const string WeatherJson = """
        {"weather":[{"main":"Clouds"}],"main":{"temp":27.5,"humidity":80},"clouds":{"all":70}}
        """;

    private static readonly DateTime T0 = new(2026, 11, 20, 9, 0, 0);

    private readonly TempDb _db = new();
    private readonly FakeClock _clock = new(T0);
    private readonly FakeIdleTimeProvider _idle = new();
    private readonly FakePowerCommandRunner _runner = new();
    private readonly StubHttpHandler _http = new();
    private readonly MainViewModel _vm;
    private readonly User _user;

    public MainViewModelTests()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(WeatherJson) };

        _user = User.Register("hermione", "secret", _clock);

        var power = new PowerMonitorService(_idle, _runner, _clock);
        var weather = new WeatherService(new HttpClient(_http), new WeatherOptions { ApiKey = "KEY", CacheMinutes = 10 }, _clock);
        _vm = new MainViewModel(_db.Repository, power, weather, _clock);
    }

    public Task InitializeAsync() => _db.Repository.SaveUserAsync(_user);

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _db.Dispose();

    private DailyQuest Quest(QuestType type) => _vm.Quests.Single(q => q.Type == type);
    private async Task<User> ReloadAsync() => (await _db.Repository.GetUserByUsernameAsync("hermione"))!;

    [Fact]
    public async Task Initialize_UnknownUser_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _vm.InitializeAsync("nobody"));
    }

    [Fact]
    public async Task RefreshDashboard_BeforeInitialize_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _vm.RefreshDashboardAsync());
    }

    [Fact]
    public async Task Initialize_LoadsUserQuestsPetSchemesWeatherAndReport()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await _vm.InitializeAsync("hermione");

        Assert.Equal("hermione", _vm.CurrentUser!.Username);
        Assert.Equal(3, _vm.Quests.Count);
        Assert.All(_vm.Quests, q => Assert.Equal(DateOnly.FromDateTime(T0), q.QuestDate));
        Assert.Equal(PetState.Lemas, _vm.CurrentUser.Pet!.CurrentState);
        Assert.Equal(2, _vm.Schemes.Count);
        Assert.Equal("Balanced", _vm.ActiveScheme!.SchemeName);
        Assert.Equal(70, _vm.CurrentWeather!.Cloudiness);
        Assert.Equal("Turunkan kecerahan ke 40%", _vm.BrightnessRecommendation);
        Assert.False(_vm.WeatherStale);
        Assert.NotNull(_vm.WeeklyReport);
        Assert.Equal(7, _vm.WeeklyReport!.GenerateWeeklyChart().Count);
        Assert.Contains(nameof(MainViewModel.CurrentUser), changed);
        Assert.Contains(nameof(MainViewModel.Quests), changed);
        Assert.Contains(nameof(MainViewModel.CurrentWeather), changed);
    }

    [Fact]
    public async Task Initialize_PersistsQuestsAndPet()
    {
        await _vm.InitializeAsync("hermione");

        var reloaded = await ReloadAsync();
        Assert.Equal(3, reloaded.Quests.Count);
        Assert.NotNull(reloaded.Pet);
    }

    [Fact]
    public async Task Initialize_WeatherDown_NoCache_IsStaleWithoutData()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await _vm.InitializeAsync("hermione");

        Assert.Null(_vm.CurrentWeather);
        Assert.True(_vm.WeatherStale);
        Assert.Equal("", _vm.BrightnessRecommendation);
        Assert.Equal(3, _vm.Quests.Count); // rest of the dashboard still works
    }

    [Fact]
    public async Task RefreshDashboard_WeatherFailsLater_KeepsLastAndMarksStale()
    {
        await _vm.InitializeAsync("hermione");
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        _clock.Advance(TimeSpan.FromMinutes(30));

        await _vm.RefreshDashboardAsync();

        Assert.Equal(70, _vm.CurrentWeather!.Cloudiness);
        Assert.True(_vm.WeatherStale);
    }

    [Fact]
    public async Task RefreshDashboardAsync_UpdatesWeather()
    {
        await _vm.InitializeAsync("hermione");
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"weather":[{"main":"Clear"}],"main":{"temp":31,"humidity":50},"clouds":{"all":10}}""")
        };
        _clock.Advance(TimeSpan.FromMinutes(11));

        await _vm.RefreshDashboardAsync();

        Assert.Equal(10, _vm.CurrentWeather!.Cloudiness);
        Assert.Equal("Kecerahan sudah optimal", _vm.BrightnessRecommendation);
    }

    [Fact]
    public async Task RefreshDashboard_UpdatesIdleAndWattEstimate()
    {
        await _vm.InitializeAsync("hermione");
        _vm.ApplyScheme(_vm.Schemes.First(s => s.SchemeName == "Power Saver"));
        _idle.Idle = TimeSpan.FromMinutes(6);

        await _vm.RefreshDashboardAsync();

        Assert.True(_vm.IsIdle);
        Assert.Equal(32.5 + 65 * 0.9, _vm.EstimatedWattSaved, 6);
    }

    [Fact]
    public async Task RefreshDashboard_BuildsWeeklyReportFromSessions()
    {
        await _vm.InitializeAsync("hermione");
        var session = new PowerSession("s1", _user.UserId, "scheme-power-saver", T0.AddHours(-2));
        session.Close(T0.AddHours(-1), 30); // 30 W for 1 h = 0.03 kWh
        await _db.Repository.SavePowerSessionAsync(session);

        await _vm.RefreshDashboardAsync();

        Assert.Equal(0.03, _vm.WeeklyReport!.TotalKwhSaved, 6);
    }

    [Fact]
    public async Task ApplyRecommendationCommand_CompletesWeatherQuestAndPersists()
    {
        await _vm.InitializeAsync("hermione");
        Assert.True(_vm.ApplyRecommendationCommand.CanExecute(null));

        await _vm.ApplyRecommendationCommand.ExecuteAsync(null);

        Assert.Equal(1, Quest(QuestType.WeatherAdaptive).CurrentProgress);
        Assert.Equal(0, Quest(QuestType.Sleep).CurrentProgress);
        Assert.Equal(1, (await ReloadAsync()).Quests.Single(q => q.Type == QuestType.WeatherAdaptive).CurrentProgress);
    }

    [Fact]
    public async Task ApplyRecommendationCommand_NoWeather_CannotExecute()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        await _vm.InitializeAsync("hermione");

        Assert.False(_vm.ApplyRecommendationCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClaimQuestCommand_IncompleteQuest_CannotExecute()
    {
        await _vm.InitializeAsync("hermione");

        Assert.False(_vm.ClaimQuestCommand.CanExecute(Quest(QuestType.Sleep)));
    }

    [Fact]
    public async Task ClaimQuestCommand_AwardsKoinUpdatesPetAndPersists()
    {
        await _vm.InitializeAsync("hermione");
        await _vm.RecordEventAsync(new QuestEvent(QuestEventKind.SleepEntered)); // sleep quest: +30 => Segar
        var sleep = Quest(QuestType.Sleep);
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.True(_vm.ClaimQuestCommand.CanExecute(sleep));

        await _vm.ClaimQuestCommand.ExecuteAsync(sleep);

        Assert.True(sleep.IsClaimed);
        Assert.Equal(30, _vm.CurrentUser!.TotalEcoKoin);
        Assert.Equal(PetState.Segar, _vm.CurrentUser.Pet!.CurrentState);
        Assert.Contains(nameof(MainViewModel.CurrentUser), changed);
        Assert.False(_vm.ClaimQuestCommand.CanExecute(sleep));

        var reloaded = await ReloadAsync();
        Assert.Equal(30, reloaded.TotalEcoKoin);
        Assert.Equal(PetState.Segar, reloaded.Pet!.CurrentState);
        Assert.True(reloaded.Quests.Single(q => q.Type == QuestType.Sleep).IsClaimed);
    }

    [Fact]
    public async Task ClaimQuestCommand_TwiceDoesNotDoublePay()
    {
        await _vm.InitializeAsync("hermione");
        await _vm.RecordEventAsync(new QuestEvent(QuestEventKind.WeatherRecommendationApplied));
        var q = Quest(QuestType.WeatherAdaptive);

        await _vm.ClaimQuestCommand.ExecuteAsync(q);
        await _vm.ClaimQuestCommand.ExecuteAsync(q);

        Assert.Equal(15, _vm.CurrentUser!.TotalEcoKoin);
        Assert.Equal(PetState.Normal, _vm.CurrentUser.Pet!.CurrentState);
    }

    [Fact]
    public async Task OnTick_AccruesPowerSaverMinutesOnlyWhenSaverActive()
    {
        await _vm.InitializeAsync("hermione"); // Balanced active: no accrual

        await _vm.OnTickAsync(TimeSpan.FromMinutes(5));
        Assert.Equal(0, Quest(QuestType.PowerSaver).CurrentProgress);

        _vm.ApplyScheme(_vm.Schemes.First(s => s.SchemeName == "Power Saver"));
        await _vm.OnTickAsync(TimeSpan.FromSeconds(90));
        Assert.Equal(1, Quest(QuestType.PowerSaver).CurrentProgress);

        await _vm.OnTickAsync(TimeSpan.FromSeconds(30)); // remainder 30s + 30s = 1 more minute
        Assert.Equal(2, Quest(QuestType.PowerSaver).CurrentProgress);
        Assert.Equal(2, (await ReloadAsync()).Quests.Single(q => q.Type == QuestType.PowerSaver).CurrentProgress);
    }

    [Fact]
    public async Task ApplyScheme_RunsPowercfgAndSetsActive()
    {
        await _vm.InitializeAsync("hermione");
        var saver = _vm.Schemes.First(s => s.SchemeName == "Power Saver");

        _vm.ApplyScheme(saver);

        Assert.Same(saver, _vm.ActiveScheme);
        Assert.Same(saver, _vm.PowerService.CurrentScheme);
        Assert.Equal(3, _runner.Calls.Count);
    }

    [Fact]
    public async Task OnTick_AcrossMidnight_LoadsFreshQuestsAndResetsPet()
    {
        await _vm.InitializeAsync("hermione");
        await _vm.RecordEventAsync(new QuestEvent(QuestEventKind.SleepEntered));
        await _vm.ClaimQuestCommand.ExecuteAsync(Quest(QuestType.Sleep));
        Assert.Equal(PetState.Segar, _vm.CurrentUser!.Pet!.CurrentState);
        var oldIds = _vm.Quests.Select(q => q.QuestId).ToHashSet();

        _clock.Advance(TimeSpan.FromDays(1));
        await _vm.OnTickAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(3, _vm.Quests.Count);
        Assert.All(_vm.Quests, q => Assert.DoesNotContain(q.QuestId, oldIds));
        Assert.All(_vm.Quests, q => Assert.Equal(DateOnly.FromDateTime(T0.AddDays(1)), q.QuestDate));
        Assert.Equal(PetState.Lemas, _vm.CurrentUser.Pet!.CurrentState);
        Assert.Equal(30, _vm.CurrentUser.TotalEcoKoin); // balance kept
    }

    [Fact]
    public async Task RecordEvent_DoesNotProgressClaimedQuest()
    {
        await _vm.InitializeAsync("hermione");
        await _vm.RecordEventAsync(new QuestEvent(QuestEventKind.SleepEntered));
        await _vm.ClaimQuestCommand.ExecuteAsync(Quest(QuestType.Sleep));

        await _vm.RecordEventAsync(new QuestEvent(QuestEventKind.SleepEntered));

        Assert.Equal(1, Quest(QuestType.Sleep).CurrentProgress);
        Assert.Equal(30, _vm.CurrentUser!.TotalEcoKoin);
    }

    [Fact]
    public async Task Command_RepositoryFailure_SetsLastErrorAndRaisesCommandFailed()
    {
        var failing = new ThrowingRepository(_db.Repository);
        var power = new PowerMonitorService(_idle, _runner, _clock);
        var weather = new WeatherService(new HttpClient(_http), new WeatherOptions { ApiKey = "KEY", CacheMinutes = 10 }, _clock);
        var vm = new MainViewModel(failing, power, weather, _clock);
        await vm.InitializeAsync("hermione");
        var raised = new TaskCompletionSource<Exception>();
        vm.CommandFailed += (_, ex) => raised.TrySetResult(ex);
        failing.FailReads = true;

        vm.RefreshCommand.Execute(null); // RefreshDashboardAsync reads the weekly report -> fails

        var ex = await raised.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Same(ex, vm.LastError);
    }

    private sealed class ThrowingRepository(IAppRepository inner) : IAppRepository
    {
        public bool FailWrites { get; set; }
        public bool FailReads { get; set; }

        private void Check(bool flag) { if (flag) throw new InvalidOperationException("db down"); }

        public Task InitializeDatabaseAsync(CancellationToken ct = default) => inner.InitializeDatabaseAsync(ct);
        public Task SaveUserAsync(User user, CancellationToken ct = default) { Check(FailWrites); return inner.SaveUserAsync(user, ct); }
        public Task<User?> GetUserByUsernameAsync(string username, CancellationToken ct = default) => inner.GetUserByUsernameAsync(username, ct);
        public Task UpdateUserProfileAsync(User user, CancellationToken ct = default) { Check(FailWrites); return inner.UpdateUserProfileAsync(user, ct); }
        public Task SavePowerSessionAsync(PowerSession session, CancellationToken ct = default) { Check(FailWrites); return inner.SavePowerSessionAsync(session, ct); }
        public Task SaveQuestProgressAsync(DailyQuest quest, CancellationToken ct = default) { Check(FailWrites); return inner.SaveQuestProgressAsync(quest, ct); }
        public Task<IReadOnlyList<PowerSession>> GetWeeklyReportDataAsync(string userId, DateTime now, CancellationToken ct = default) { Check(FailReads); return inner.GetWeeklyReportDataAsync(userId, now, ct); }
        public Task<IReadOnlyList<PowerScheme>> GetPowerSchemesAsync(CancellationToken ct = default) => inner.GetPowerSchemesAsync(ct);
        public Task<IReadOnlyList<DailyQuest>> EnsureDailyQuestsAsync(string userId, DateOnly date, CancellationToken ct = default) => inner.EnsureDailyQuestsAsync(userId, date, ct);
    }
}
