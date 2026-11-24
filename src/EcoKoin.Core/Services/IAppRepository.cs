using EcoKoin.Models;

namespace EcoKoin.Services
{
    /// <summary>
    /// Persistence boundary of EcoKoin. The product implementation talks to a remote database (PostgreSQL);
    /// tests use an in-memory/temp-file stand-in. Every call is asynchronous because the database is remote.
    /// Spec: docs/03-domain-model.md (repository) and docs/04-database.md.
    /// Implementations must be safe to call from several tasks at once (no shared open connection).
    /// </summary>
    public interface IAppRepository
    {
        /// <summary>
        /// Creates the schema if missing and seeds the power schemes (Power Saver = 50, Balanced = 80,
        /// ids "scheme-power-saver" / "scheme-balanced"). Idempotent: safe to call on every start.
        /// </summary>
        Task InitializeDatabaseAsync(CancellationToken ct = default);

        /// <summary>
        /// Upserts the user (by user id), the pet (if <see cref="User.Pet"/> is set) and all
        /// <see cref="User.Quests"/> in one transaction (all or nothing).
        /// A null <see cref="User.DisplayName"/> never overwrites an already stored display name.
        /// Throws <see cref="DuplicateUsernameException"/> when another user already has the same username.
        /// </summary>
        Task SaveUserAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Loads the user with <see cref="User.Pet"/> and <see cref="User.Quests"/>; null if the username is unknown.
        /// The quests are those of the latest quest_date stored for the user (i.e. "today's" quests once
        /// <see cref="EnsureDailyQuestsAsync"/> ran), in insertion order.
        /// </summary>
        Task<User?> GetUserByUsernameAsync(string username, CancellationToken ct = default);

        /// <summary>
        /// Updates total EcoKoin, the pet and the quests of an existing user in one transaction.
        /// Throws <see cref="InvalidOperationException"/> if the user does not exist.
        /// </summary>
        Task UpdateUserProfileAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Inserts or updates a power session by session id (EndTime may be null for a running session).
        /// An unknown user id or scheme id is rejected by the database (foreign key) and surfaces as an exception.
        /// </summary>
        Task SavePowerSessionAsync(PowerSession session, CancellationToken ct = default);

        /// <summary>
        /// Upserts a quest row by quest id. For an existing row only current_progress and is_claimed change.
        /// An unknown user id is rejected by the database (foreign key) and surfaces as an exception.
        /// </summary>
        Task SaveQuestProgressAsync(DailyQuest quest, CancellationToken ct = default);

        /// <summary>
        /// Closed and running sessions of <paramref name="userId"/> whose start time is within the 7 days
        /// up to <paramref name="now"/> (start &gt;= now - 7 days), ordered by start time ascending.
        /// </summary>
        Task<IReadOnlyList<PowerSession>> GetWeeklyReportDataAsync(string userId, DateTime now, CancellationToken ct = default);

        /// <summary>All power schemes ordered by cpu_max_performance (lowest first).</summary>
        Task<IReadOnlyList<PowerScheme>> GetPowerSchemesAsync(CancellationToken ct = default);

        /// <summary>
        /// Creates the 3 default quests (<see cref="DailyQuest.CreateDefaultsFor"/>) for <paramref name="date"/>
        /// if the user has none for that date, then returns that day's quests in insertion order.
        /// Atomic. An unknown user id surfaces as an exception and creates nothing.
        /// </summary>
        Task<IReadOnlyList<DailyQuest>> EnsureDailyQuestsAsync(string userId, DateOnly date, CancellationToken ct = default);

        // ---- Dashboard, quest and analytics queries ----
        // Behaviour is pinned by RepositoryDashboardContractTests, which every implementation must pass.

        /// <summary>Records one Power Saver sample minute for the quest; progress only moves forward (see original RecordPowerSaverMinute).</summary>
        Task RecordPowerSaverMinuteAsync(string userId, string questId, DateOnly date, long sampleMinute, CancellationToken ct = default);

        /// <summary>Total tracked Power Saver minutes of the user over the inclusive date range.</summary>
        Task<long> GetPowerSaverMinutesAsync(string userId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

        /// <summary>Atomically claims the Power Saver quest; true only when this call claimed it.</summary>
        Task<bool> TryClaimPowerSaverQuestAsync(string userId, string questId, DateOnly date, CancellationToken ct = default);

        /// <summary>Marks the weather-adaptive quest as applied (progress); true when this call changed it.</summary>
        Task<bool> TryApplyWeatherQuestAsync(string userId, DateOnly date, CancellationToken ct = default);

        /// <summary>Atomically claims the weather-adaptive quest; true only when this call claimed it.</summary>
        Task<bool> TryClaimWeatherQuestAsync(string userId, DateOnly date, CancellationToken ct = default);

        /// <summary>Koin earned (claimed quests) on <paramref name="date"/>.</summary>
        Task<int> GetCoinsEarnedTodayAsync(string userId, DateOnly date, CancellationToken ct = default);

        /// <summary>Tracked Power Saver minutes per day over the inclusive date range.</summary>
        Task<IReadOnlyDictionary<DateOnly, long>> GetDailyPowerSaverMinutesAsync(string userId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

        /// <summary>Sessions with start_time in [from, to), ordered by start_time; open sessions included.</summary>
        Task<IReadOnlyList<PowerSession>> GetPowerSessionsAsync(string userId, DateTime fromInclusive, DateTime toExclusive, CancellationToken ct = default);

        /// <summary>users.total_ecokoin of the user; 0 if the user does not exist.</summary>
        Task<int> GetTotalEcoKoinAsync(string userId, CancellationToken ct = default);

        /// <summary>Sum of reward_coin of the user's claimed quests with quest_date = <paramref name="date"/>; 0 if none.</summary>
        Task<int> GetKoinEarnedOnAsync(string userId, DateOnly date, CancellationToken ct = default);

        /// <summary>Claimed koin per calendar day: one entry per day in [from, to] inclusive, ascending, 0 where none; inverted range gives an empty list.</summary>
        Task<IReadOnlyList<(DateOnly Date, int Koin)>> GetKoinEarnedPerDayAsync(string userId, DateOnly fromInclusive, DateOnly toInclusive, CancellationToken ct = default);

        /// <summary>Atomically claims a quest of any type (owned by user, dated <paramref name="date"/>, unclaimed, progress &gt;= target) and adds its reward to the user's balance in the same transaction; true only when this call claimed it.</summary>
        Task<bool> TryClaimQuestAsync(string userId, string questId, DateOnly date, CancellationToken ct = default);

        /// <summary>Adds progress to the user's Sleep or WeatherAdaptive quest of <paramref name="date"/> (clamped to target, only while unclaimed). Returns the new progress, or -1 if missing/claimed. Throws ArgumentException for PowerSaver, ArgumentOutOfRangeException for amount &lt;= 0.</summary>
        Task<int> AddQuestProgressAsync(string userId, QuestType type, DateOnly date, int amount, CancellationToken ct = default);

        /// <summary>Loads the user's pet (creating it if missing), refreshes it with UpdateState(koin earned on <paramref name="today"/>), persists and returns it.</summary>
        Task<EcoPet> GetOrCreatePetAsync(string userId, DateOnly today, CancellationToken ct = default);
    }
}
