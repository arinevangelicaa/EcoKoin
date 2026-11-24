using System.Globalization;
using EcoKoin.Models;
using EcoKoin.Services;
using Microsoft.Data.Sqlite;

namespace EcoKoin.Core.Tests.Support
{
    /// <summary>
    /// SQLite stand-in for <see cref="IAppRepository"/>, used by tests only (the product uses a remote PostgreSQL database).
    /// Spec: docs/03-domain-model.md and docs/04-database.md.
    /// Rules: PRAGMA foreign_keys = ON on every connection; parameterized queries only; transactions for multi-table writes.
    /// Dates are stored as "yyyy-MM-dd HH:mm:ss" (invariant culture); quest_date as "yyyy-MM-dd".
    /// </summary>
    public sealed class SqliteTestRepository : IAppRepository
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string DateFormat = "yyyy-MM-dd";

        private readonly string _connectionString;

        public SqliteTestRepository(string connectionString = "Data Source=ecokoin-test.db")
        {
            _connectionString = connectionString;
        }

        /// <summary>Runs Support/sqlite/schema.sql then seed.sql (both from AppContext.BaseDirectory). Idempotent.</summary>
        private void InitializeDatabaseCore()
        {
            EnsureDatabaseDirectory();

            string dbDir = Path.Combine(AppContext.BaseDirectory, "Support", "sqlite");
            string schemaSql = File.ReadAllText(Path.Combine(dbDir, "schema.sql"));
            string seedSql = File.ReadAllText(Path.Combine(dbDir, "seed.sql"));

            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = tx;
            command.CommandText = schemaSql;
            command.ExecuteNonQuery();

            // Databases created before WPF account registration have no display_name column.
            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('USERS') WHERE name = 'display_name';";
            if ((long)command.ExecuteScalar()! == 0)
            {
                command.CommandText = "ALTER TABLE USERS ADD COLUMN display_name TEXT;";
                command.ExecuteNonQuery();
            }

            command.CommandText = seedSql;
            command.ExecuteNonQuery();
            tx.Commit();
        }

        /// <summary>
        /// Upserts USERS (+ ECO_PETS if Pet != null) and all <see cref="User.Quests"/> in one transaction.
        /// A different user with the same username violates UNIQUE and is translated to <see cref="DuplicateUsernameException"/>.
        /// </summary>
        private void SaveUserCore(User user)
        {
            try
            {
                SaveUserUnchecked(user);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 && ex.Message.Contains("USERS.username"))
            {
                throw new DuplicateUsernameException(user.Username, ex);
            }
        }

        private void SaveUserUnchecked(User user)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO USERS (user_id, username, password_hash, total_ecokoin, join_date, display_name)
                    VALUES ($id, $username, $hash, $coins, $join, $displayName)
                    ON CONFLICT(user_id) DO UPDATE SET
                        username = excluded.username,
                        password_hash = excluded.password_hash,
                        total_ecokoin = excluded.total_ecokoin,
                        join_date = excluded.join_date,
                        display_name = COALESCE(excluded.display_name, USERS.display_name);";
                cmd.Parameters.AddWithValue("$id", user.UserId);
                cmd.Parameters.AddWithValue("$username", user.Username);
                cmd.Parameters.AddWithValue("$hash", user.PasswordHash);
                cmd.Parameters.AddWithValue("$coins", user.TotalEcoKoin);
                cmd.Parameters.AddWithValue("$join", FormatDateTime(user.JoinDate));
                cmd.Parameters.AddWithValue("$displayName", (object?)user.DisplayName ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            UpsertPet(connection, tx, user);
            foreach (var quest in user.Quests)
                UpsertQuest(connection, tx, quest);

            tx.Commit();
        }

        /// <summary>
        /// Loads the user with Pet and Quests; null if not found. Quests are the ones of the latest
        /// quest_date stored for the user (i.e. "today's" quests once <see cref="EnsureDailyQuestsAsync"/> ran).
        /// </summary>
        private User? GetUserByUsernameCore(string u)
        {
            using var connection = OpenConnection();

            User user;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT user_id, username, password_hash, total_ecokoin, join_date, display_name
                                    FROM USERS WHERE username = $username;";
                cmd.Parameters.AddWithValue("$username", u);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return null;

                user = new User(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    ParseDateTime(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : reader.GetString(5));
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT pet_id, happiness_level, current_state
                                    FROM ECO_PETS WHERE user_id = $uid;";
                cmd.Parameters.AddWithValue("$uid", user.UserId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    user.Pet = new EcoPet(
                        reader.GetString(0),
                        user.UserId,
                        reader.GetInt32(1),
                        Enum.Parse<PetState>(reader.GetString(2)));
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT quest_id, quest_type, description, target_value, reward_coin,
                                           quest_date, current_progress, is_claimed
                                    FROM DAILY_QUESTS
                                    WHERE user_id = $uid
                                      AND quest_date = (SELECT MAX(quest_date) FROM DAILY_QUESTS WHERE user_id = $uid)
                                    ORDER BY rowid;";
                cmd.Parameters.AddWithValue("$uid", user.UserId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    user.Quests.Add(ReadQuest(reader, user.UserId));
            }

            return user;
        }

        /// <summary>
        /// Updates total_ecokoin, the pet and the quests of an existing user in one transaction.
        /// Throws <see cref="InvalidOperationException"/> if the user does not exist.
        /// </summary>
        private void UpdateUserProfileCore(User user)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE USERS SET total_ecokoin = $coins WHERE user_id = $id;";
                cmd.Parameters.AddWithValue("$coins", user.TotalEcoKoin);
                cmd.Parameters.AddWithValue("$id", user.UserId);
                if (cmd.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException($"User '{user.UserId}' does not exist.");
            }

            UpsertPet(connection, tx, user);
            foreach (var quest in user.Quests)
                UpsertQuest(connection, tx, quest);

            tx.Commit();
        }

        /// <summary>Insert or update POWER_SESSIONS row (end_time may be NULL). FK violations throw <see cref="SqliteException"/>.</summary>
        private void SavePowerSessionCore(PowerSession session)
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO POWER_SESSIONS (session_id, user_id, scheme_id, start_time, end_time, watt_saved)
                VALUES ($id, $uid, $sid, $start, $end, $watt)
                ON CONFLICT(session_id) DO UPDATE SET
                    user_id = excluded.user_id,
                    scheme_id = excluded.scheme_id,
                    start_time = excluded.start_time,
                    end_time = excluded.end_time,
                    watt_saved = excluded.watt_saved;";
            cmd.Parameters.AddWithValue("$id", session.SessionId);
            cmd.Parameters.AddWithValue("$uid", session.UserId);
            cmd.Parameters.AddWithValue("$sid", session.SchemeId);
            cmd.Parameters.AddWithValue("$start", FormatDateTime(session.StartTime));
            cmd.Parameters.AddWithValue("$end", session.EndTime.HasValue ? FormatDateTime(session.EndTime.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("$watt", session.WattSaved);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Upserts the quest row; on an existing row current_progress and is_claimed are updated.</summary>
        private void SaveQuestProgressCore(DailyQuest quest)
        {
            using var connection = OpenConnection();
            UpsertQuest(connection, null, quest);
        }

        /// <summary>Closed and running sessions of <paramref name="userId"/> with start_time in the last 7 days from <paramref name="now"/>, ordered by start_time.</summary>
        private IReadOnlyList<PowerSession> GetWeeklyReportDataCore(string userId, DateTime now)
        {
            var result = new List<PowerSession>();

            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT session_id, user_id, scheme_id, start_time, end_time, watt_saved
                                FROM POWER_SESSIONS
                                WHERE user_id = $uid AND start_time >= $since
                                ORDER BY start_time ASC;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$since", FormatDateTime(now.AddDays(-7)));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new PowerSession(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    ParseDateTime(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : ParseDateTime(reader.GetString(4)),
                    reader.GetDouble(5)));
            }

            return result;
        }

        /// <summary>All power schemes ordered by cpu_max_performance (lowest first).</summary>
        private IReadOnlyList<PowerScheme> GetPowerSchemesCore()
        {
            var result = new List<PowerScheme>();

            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT scheme_id, scheme_name, cpu_max_performance
                                FROM POWER_SCHEMES ORDER BY cpu_max_performance, scheme_id;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new PowerScheme(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));

            return result;
        }

        /// <summary>Creates the 3 default quests for <paramref name="date"/> if the user has none for that date; returns that day's quests.</summary>
        private IReadOnlyList<DailyQuest> EnsureDailyQuestsCore(string userId, DateOnly date)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();

            long existing;
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT COUNT(*) FROM DAILY_QUESTS WHERE user_id = $uid AND quest_date = $date;";
                cmd.Parameters.AddWithValue("$uid", userId);
                cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
                existing = (long)cmd.ExecuteScalar()!;
            }

            if (existing == 0)
            {
                foreach (var quest in DailyQuest.CreateDefaultsFor(userId, date))
                    UpsertQuest(connection, tx, quest);
            }

            var quests = new List<DailyQuest>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"SELECT quest_id, quest_type, description, target_value, reward_coin,
                                           quest_date, current_progress, is_claimed
                                    FROM DAILY_QUESTS
                                    WHERE user_id = $uid AND quest_date = $date
                                    ORDER BY rowid;";
                cmd.Parameters.AddWithValue("$uid", userId);
                cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    quests.Add(ReadQuest(reader, userId));
            }

            tx.Commit();
            return quests;
        }

        // ---- IAppRepository (async facade over the synchronous SQLite work) -------------------

        public Task InitializeDatabaseAsync(CancellationToken ct = default)
            => Run(ct, InitializeDatabaseCore);

        public Task SaveUserAsync(User user, CancellationToken ct = default)
            => Run(ct, () => SaveUserCore(user));

        public Task<User?> GetUserByUsernameAsync(string username, CancellationToken ct = default)
            => Run(ct, () => GetUserByUsernameCore(username));

        public Task UpdateUserProfileAsync(User user, CancellationToken ct = default)
            => Run(ct, () => UpdateUserProfileCore(user));

        public Task SavePowerSessionAsync(PowerSession session, CancellationToken ct = default)
            => Run(ct, () => SavePowerSessionCore(session));

        public Task SaveQuestProgressAsync(DailyQuest quest, CancellationToken ct = default)
            => Run(ct, () => SaveQuestProgressCore(quest));

        public Task<IReadOnlyList<PowerSession>> GetWeeklyReportDataAsync(string userId, DateTime now, CancellationToken ct = default)
            => Run(ct, () => GetWeeklyReportDataCore(userId, now));

        public Task<IReadOnlyList<PowerScheme>> GetPowerSchemesAsync(CancellationToken ct = default)
            => Run(ct, GetPowerSchemesCore);

        public Task<IReadOnlyList<DailyQuest>> EnsureDailyQuestsAsync(string userId, DateOnly date, CancellationToken ct = default)
            => Run(ct, () => EnsureDailyQuestsCore(userId, date));

        /// <summary>Runs the work synchronously; exceptions are delivered through the returned task.</summary>
        private static Task Run(CancellationToken ct, Action work)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                work();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private static Task<T> Run<T>(CancellationToken ct, Func<T> work)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(work());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        // ---- helpers -------------------------------------------------------------------------

        /// <summary>Opens a connection and enables foreign keys (SQLite default is off, per connection).</summary>
        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return connection;
        }

        /// <summary>Creates the folder of a file-based database if it does not exist yet.</summary>
        private void EnsureDatabaseDirectory()
        {
            var builder = new SqliteConnectionStringBuilder(_connectionString);
            string dataSource = builder.DataSource;
            if (string.IsNullOrWhiteSpace(dataSource)
                || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || builder.Mode == SqliteOpenMode.Memory)
                return;

            string? dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        private static void UpsertPet(SqliteConnection connection, SqliteTransaction tx, User user)
        {
            if (user.Pet == null)
                return;

            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO ECO_PETS (pet_id, user_id, happiness_level, current_state)
                VALUES ($pid, $uid, $happy, $state)
                ON CONFLICT(pet_id) DO UPDATE SET
                    happiness_level = excluded.happiness_level,
                    current_state = excluded.current_state;";
            cmd.Parameters.AddWithValue("$pid", user.Pet.PetId);
            cmd.Parameters.AddWithValue("$uid", user.UserId);
            cmd.Parameters.AddWithValue("$happy", user.Pet.HappinessLevel);
            cmd.Parameters.AddWithValue("$state", user.Pet.CurrentState.ToString());
            cmd.ExecuteNonQuery();
        }

        private static void UpsertQuest(SqliteConnection connection, SqliteTransaction? tx, DailyQuest quest)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO DAILY_QUESTS (quest_id, user_id, quest_type, description, target_value,
                                          current_progress, reward_coin, is_claimed, quest_date)
                VALUES ($id, $uid, $type, $desc, $target, $progress, $reward, $claimed, $date)
                ON CONFLICT(quest_id) DO UPDATE SET
                    current_progress = excluded.current_progress,
                    is_claimed = excluded.is_claimed;";
            cmd.Parameters.AddWithValue("$id", quest.QuestId);
            cmd.Parameters.AddWithValue("$uid", quest.UserId);
            cmd.Parameters.AddWithValue("$type", quest.Type.ToString());
            cmd.Parameters.AddWithValue("$desc", quest.Description);
            cmd.Parameters.AddWithValue("$target", quest.TargetValue);
            cmd.Parameters.AddWithValue("$progress", quest.CurrentProgress);
            cmd.Parameters.AddWithValue("$reward", quest.RewardCoin);
            cmd.Parameters.AddWithValue("$claimed", quest.IsClaimed ? 1 : 0);
            cmd.Parameters.AddWithValue("$date", quest.QuestDate.ToString(DateFormat, CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        /// <summary>Reads columns: quest_id, quest_type, description, target_value, reward_coin, quest_date, current_progress, is_claimed.</summary>
        private static DailyQuest ReadQuest(SqliteDataReader reader, string userId)
        {
            return DailyQuest.Create(
                Enum.Parse<QuestType>(reader.GetString(1)),
                reader.GetString(0),
                userId,
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                DateOnly.ParseExact(reader.GetString(5), DateFormat, CultureInfo.InvariantCulture),
                reader.GetInt32(6),
                reader.GetInt32(7) != 0);
        }

        private static string FormatDateTime(DateTime value) =>
            value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

        private static DateTime ParseDateTime(string value) =>
            DateTime.ParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture);
    }
}
