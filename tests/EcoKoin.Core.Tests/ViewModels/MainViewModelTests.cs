using System.Net;
using System.Net.Http;
using EcoKoin.Core.Tests.Services;
using EcoKoin.Models;
using EcoKoin.Services;
using EcoKoin.ViewModels;

namespace EcoKoin.Core.Tests.ViewModels;

public class MainViewModelTests : IDisposable
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
        _db.Repository.SaveUser(_user);

        var power = new PowerMonitorService(_idle, _runner, _clock);
        var weather = new WeatherService(new HttpClient(_http), new WeatherOptions { ApiKey = "KEY", CacheMinutes = 10 }, _clock);
        _vm = new MainViewModel(_db.Repository, power, weather, _clock);
    }

    public void Dispose() => _db.Dispose();

    private DailyQuest Quest(QuestType type) => _vm.Quests.Single(q => q.Type == type);
    private User Reload() => _db.Repository.GetUserByUsername("hermione")!;

    [Fact]
    public void Initialize_UnknownUser_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _vm.Initialize("nobody"));
    }

    [Fact]
    public void RefreshDashboard_BeforeInitialize_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _vm.RefreshDashboard());
    }

    [Fact]
    public void Initialize_LoadsUserQuestsPetSchemesWeatherAndReport()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _vm.Initialize("hermione");

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
    public void Initialize_PersistsQuestsAndPet()
    {
        _vm.Initialize("hermione");

        var reloaded = Reload();
        Assert.Equal(3, reloaded.Quests.Count);
        Assert.NotNull(reloaded.Pet);
    }

    [Fact]
    public void Initialize_WeatherDown_NoCache_IsStaleWithoutData()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        _vm.Initialize("hermione");

        Assert.Null(_vm.CurrentWeather);
        Assert.True(_vm.WeatherStale);
        Assert.Equal("", _vm.BrightnessRecommendation);
        Assert.Equal(3, _vm.Quests.Count); // rest of the dashboard still works
    }

    [Fact]
    public void RefreshDashboard_WeatherFailsLater_KeepsLastAndMarksStale()
    {
        _vm.Initialize("hermione");
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        _clock.Advance(TimeSpan.FromMinutes(30));

        _vm.RefreshDashboard();

        Assert.Equal(70, _vm.CurrentWeather!.Cloudiness);
        Assert.True(_vm.WeatherStale);
    }

    [Fact]
    public async Task RefreshDashboardAsync_UpdatesWeather()
    {
        _vm.Initialize("hermione");
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
    public void RefreshDashboard_UpdatesIdleAndWattEstimate()
    {
        _vm.Initialize("hermione");
        _vm.ApplyScheme(_vm.Schemes.First(s => s.SchemeName == "Power Saver"));
        _idle.Idle = TimeSpan.FromMinutes(6);

        _vm.RefreshDashboard();

        Assert.True(_vm.IsIdle);
        Assert.Equal(32.5 + 65 * 0.9, _vm.EstimatedWattSaved, 6);
    }

    [Fact]
    public void RefreshDashboard_BuildsWeeklyReportFromSessions()
    {
        _vm.Initialize("hermione");
        var session = new PowerSession("s1", _user.UserId, "scheme-power-saver", T0.AddHours(-2));
        session.Close(T0.AddHours(-1), 30); // 30 W for 1 h = 0.03 kWh
        _db.Repository.SavePowerSession(session);

        _vm.RefreshDashboard();

        Assert.Equal(0.03, _vm.WeeklyReport!.TotalKwhSaved, 6);
    }

    [Fact]
    public void ApplyRecommendationCommand_CompletesWeatherQuestAndPersists()
    {
        _vm.Initialize("hermione");
        Assert.True(_vm.ApplyRecommendationCommand.CanExecute(null));

        _vm.ApplyRecommendationCommand.Execute(null);

        Assert.Equal(1, Quest(QuestType.WeatherAdaptive).CurrentProgress);
        Assert.Equal(0, Quest(QuestType.Sleep).CurrentProgress);
        Assert.Equal(1, Reload().Quests.Single(q => q.Type == QuestType.WeatherAdaptive).CurrentProgress);
    }

    [Fact]
    public void ApplyRecommendationCommand_NoWeather_CannotExecute()
    {
        _http.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        _vm.Initialize("hermione");

        Assert.False(_vm.ApplyRecommendationCommand.CanExecute(null));
    }

    [Fact]
    public void ClaimQuestCommand_IncompleteQuest_CannotExecute()
    {
        _vm.Initialize("hermione");

        Assert.False(_vm.ClaimQuestCommand.CanExecute(Quest(QuestType.Sleep)));
    }

    [Fact]
    public void ClaimQuestCommand_AwardsKoinUpdatesPetAndPersists()
    {
        _vm.Initialize("hermione");
        _vm.RecordEvent(new QuestEvent(QuestEventKind.SleepEntered)); // sleep quest: +30 => Segar
        var sleep = Quest(QuestType.Sleep);
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.True(_vm.ClaimQuestCommand.CanExecute(sleep));

        _vm.ClaimQuestCommand.Execute(sleep);

        Assert.True(sleep.IsClaimed);
        Assert.Equal(30, _vm.CurrentUser!.TotalEcoKoin);
        Assert.Equal(PetState.Segar, _vm.CurrentUser.Pet!.CurrentState);
        Assert.Contains(nameof(MainViewModel.CurrentUser), changed);
        Assert.False(_vm.ClaimQuestCommand.CanExecute(sleep));

        var reloaded = Reload();
        Assert.Equal(30, reloaded.TotalEcoKoin);
        Assert.Equal(PetState.Segar, reloaded.Pet!.CurrentState);
        Assert.True(reloaded.Quests.Single(q => q.Type == QuestType.Sleep).IsClaimed);
    }

    [Fact]
    public void ClaimQuestCommand_TwiceDoesNotDoublePay()
    {
        _vm.Initialize("hermione");
        _vm.RecordEvent(new QuestEvent(QuestEventKind.WeatherRecommendationApplied));
        var q = Quest(QuestType.WeatherAdaptive);

        _vm.ClaimQuestCommand.Execute(q);
        _vm.ClaimQuestCommand.Execute(q);

        Assert.Equal(15, _vm.CurrentUser!.TotalEcoKoin);
        Assert.Equal(PetState.Normal, _vm.CurrentUser.Pet!.CurrentState);
    }

    [Fact]
    public void OnTick_AccruesPowerSaverMinutesOnlyWhenSaverActive()
    {
        _vm.Initialize("hermione"); // Balanced active: no accrual

        _vm.OnTick(TimeSpan.FromMinutes(5));
        Assert.Equal(0, Quest(QuestType.PowerSaver).CurrentProgress);

        _vm.ApplyScheme(_vm.Schemes.First(s => s.SchemeName == "Power Saver"));
        _vm.OnTick(TimeSpan.FromSeconds(90));
        Assert.Equal(1, Quest(QuestType.PowerSaver).CurrentProgress);

        _vm.OnTick(TimeSpan.FromSeconds(30)); // remainder 30s + 30s = 1 more minute
        Assert.Equal(2, Quest(QuestType.PowerSaver).CurrentProgress);
        Assert.Equal(2, Reload().Quests.Single(q => q.Type == QuestType.PowerSaver).CurrentProgress);
    }

    [Fact]
    public void ApplyScheme_RunsPowercfgAndSetsActive()
    {
        _vm.Initialize("hermione");
        var saver = _vm.Schemes.First(s => s.SchemeName == "Power Saver");

        _vm.ApplyScheme(saver);

        Assert.Same(saver, _vm.ActiveScheme);
        Assert.Same(saver, _vm.PowerService.CurrentScheme);
        Assert.Equal(3, _runner.Calls.Count);
    }

    [Fact]
    public void OnTick_AcrossMidnight_LoadsFreshQuestsAndResetsPet()
    {
        _vm.Initialize("hermione");
        _vm.RecordEvent(new QuestEvent(QuestEventKind.SleepEntered));
        _vm.ClaimQuestCommand.Execute(Quest(QuestType.Sleep));
        Assert.Equal(PetState.Segar, _vm.CurrentUser!.Pet!.CurrentState);
        var oldIds = _vm.Quests.Select(q => q.QuestId).ToHashSet();

        _clock.Advance(TimeSpan.FromDays(1));
        _vm.OnTick(TimeSpan.FromMinutes(1));

        Assert.Equal(3, _vm.Quests.Count);
        Assert.All(_vm.Quests, q => Assert.DoesNotContain(q.QuestId, oldIds));
        Assert.All(_vm.Quests, q => Assert.Equal(DateOnly.FromDateTime(T0.AddDays(1)), q.QuestDate));
        Assert.Equal(PetState.Lemas, _vm.CurrentUser.Pet!.CurrentState);
        Assert.Equal(30, _vm.CurrentUser.TotalEcoKoin); // balance kept
    }

    [Fact]
    public void RecordEvent_DoesNotProgressClaimedQuest()
    {
        _vm.Initialize("hermione");
        _vm.RecordEvent(new QuestEvent(QuestEventKind.SleepEntered));
        _vm.ClaimQuestCommand.Execute(Quest(QuestType.Sleep));

        _vm.RecordEvent(new QuestEvent(QuestEventKind.SleepEntered));

        Assert.Equal(1, Quest(QuestType.Sleep).CurrentProgress);
        Assert.Equal(30, _vm.CurrentUser!.TotalEcoKoin);
    }
}
