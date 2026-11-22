using System.Globalization;
using EcoKoin.Models;
using Microsoft.Data.Sqlite;

namespace EcoKoin.Services
{
    /// <summary>
    /// SQLite persistence. Spec: docs/03-domain-model.md (SqliteRepository) and docs/04-database.md.
    /// Rules: PRAGMA foreign_keys = ON on every connection; parameterized queries only; transactions for multi-table writes.
    /// Dates are stored as "yyyy-MM-dd HH:mm:ss" (invariant culture); quest_date as "yyyy-MM-dd".
    /// </summary>
    public class SqliteRepository
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string DateFormat = "yyyy-MM-dd";

        private readonly string _connectionString;

        public SqliteRepository(string connectionString = "Data Source=ecokoin.db")
        {
            _connectionString = connectionString;
        }

        /// <summary>Runs Database/schema.sql then Database/seed.sql (both from AppContext.BaseDirectory). Idempotent.</summary>
        public void InitializeDatabase()
        {
            EnsureDatabaseDirectory();

            string dbDir = Path.Combine(AppContext.BaseDirectory, "Database");
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

            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('POWER_SAVER_PROGRESS') WHERE name = 'tracked_minutes';";
            if ((long)command.ExecuteScalar()! == 0)
            {
                command.CommandText = "ALTER TABLE POWER_SAVER_PROGRESS ADD COLUMN tracked_minutes INTEGER;";
                command.ExecuteNonQuery();
            }

            command.CommandText = seedSql;
            command.ExecuteNonQuery();
            tx.Commit();
        }

        /// <summary>
        /// Upserts USERS (+ ECO_PETS if Pet != null) and all <see cref="User.Quests"/> in one transaction.
        /// A different user with the same username violates UNIQUE and surfaces as <see cref="SqliteException"/>.
        /// </summary>
        public void SaveUser(User user)
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
        /// quest_date stored for the user (i.e. "today's" quests once <see cref="EnsureDailyQuests"/> ran).
        /// </summary>
        public User? GetUserByUsername(string u)
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
        public void UpdateUserProfile(User user)
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
        public void SavePowerSession(PowerSession session)
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
        public void SaveQuestProgress(DailyQuest quest)
        {
            using var connection = OpenConnection();
            UpsertQuest(connection, null, quest);
        }

        /// <summary>Closed and running sessions of <paramref name="userId"/> with start_time in the last 7 days from <paramref name="now"/>, ordered by start_time.</summary>
        public IReadOnlyList<PowerSession> GetWeeklyReportData(string userId, DateTime now)
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
        public IReadOnlyList<PowerScheme> GetPowerSchemes()
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
        public IReadOnlyList<DailyQuest> EnsureDailyQuests(string userId, DateOnly date)
        {
            using var connection = OpenConnection();
            // BEGIN IMMEDIATE: this reads (COUNT) and then writes. With a deferred transaction two concurrent
            // callers (quest list, Power Saver tracker, quest events) both read, then one write fails at once
            // with SQLITE_BUSY ("database is locked") instead of waiting for the other to finish.
            using var tx = connection.BeginTransaction(deferred: false);

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

        public void RecordPowerSaverMinute(string userId, string questId, DateOnly date, long sampleMinute)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO POWER_SAVER_PROGRESS (quest_id, last_sample_minute, tracked_minutes)
                SELECT quest_id, $minute, current_progress + 1 FROM DAILY_QUESTS
                WHERE quest_id = $quest AND user_id = $user AND quest_date = $date
                  AND quest_type = 'PowerSaver'
                ON CONFLICT(quest_id) DO UPDATE SET last_sample_minute = excluded.last_sample_minute,
                    tracked_minutes = COALESCE(POWER_SAVER_PROGRESS.tracked_minutes + 1, excluded.tracked_minutes)
                WHERE excluded.last_sample_minute > POWER_SAVER_PROGRESS.last_sample_minute;";
            AddPowerSaverParameters(cmd, userId, questId, date);
            cmd.Parameters.AddWithValue("$minute", sampleMinute);
            if (cmd.ExecuteNonQuery() == 1)
            {
                cmd.CommandText = @"UPDATE DAILY_QUESTS SET current_progress = current_progress + 1
                                    WHERE quest_id = $quest AND is_claimed = 0 AND current_progress < target_value;";
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public long GetPowerSaverMinutes(string userId, DateOnly startDate, DateOnly endDate)
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COALESCE(SUM(COALESCE(p.tracked_minutes, q.current_progress)), 0)
                FROM DAILY_QUESTS q
                LEFT JOIN POWER_SAVER_PROGRESS p ON p.quest_id = q.quest_id
                WHERE q.user_id = $user AND q.quest_type = 'PowerSaver'
                  AND q.quest_date BETWEEN $start AND $end;";
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$start", startDate.ToString(DateFormat, CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$end", endDate.ToString(DateFormat, CultureInfo.InvariantCulture));
            return (long)cmd.ExecuteScalar()!;
        }

        public bool TryClaimPowerSaverQuest(string userId, string questId, DateOnly date)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                UPDATE DAILY_QUESTS SET is_claimed = 1
                WHERE quest_id = $quest AND user_id = $user AND quest_date = $date
                  AND quest_type = 'PowerSaver' AND is_claimed = 0 AND current_progress >= target_value
                RETURNING reward_coin;";
            AddPowerSaverParameters(cmd, userId, questId, date);
            if (cmd.ExecuteScalar() is not long reward)
                return false;

            cmd.CommandText = "UPDATE USERS SET total_ecokoin = total_ecokoin + $reward WHERE user_id = $user;";
            cmd.Parameters.AddWithValue("$reward", reward);
            cmd.ExecuteNonQuery();

            UpdatePetStateInTransaction(connection, tx, userId, date);

            tx.Commit();
            return true;
        }

        public bool TryApplyWeatherQuest(string userId, DateOnly date)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                UPDATE DAILY_QUESTS
                SET current_progress = target_value
                WHERE user_id = $user AND quest_date = $date AND quest_type = 'WeatherAdaptive' AND is_claimed = 0;";
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
            int rows = cmd.ExecuteNonQuery();
            tx.Commit();
            return rows > 0;
        }

        public bool TryClaimWeatherQuest(string userId, DateOnly date)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                UPDATE DAILY_QUESTS SET is_claimed = 1
                WHERE user_id = $user AND quest_date = $date
                  AND quest_type = 'WeatherAdaptive' AND is_claimed = 0 AND current_progress >= target_value
                RETURNING reward_coin;";
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
            if (cmd.ExecuteScalar() is not long reward)
                return false;

            cmd.CommandText = "UPDATE USERS SET total_ecokoin = total_ecokoin + $reward WHERE user_id = $user;";
            cmd.Parameters.AddWithValue("$reward", reward);
            cmd.ExecuteNonQuery();

            UpdatePetStateInTransaction(connection, tx, userId, date);

            tx.Commit();
            return true;
        }

        public int GetCoinsEarnedToday(string userId, DateOnly date)
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COALESCE(SUM(reward_coin), 0)
                FROM DAILY_QUESTS
                WHERE user_id = $user AND quest_date = $date AND is_claimed = 1;";
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public IReadOnlyDictionary<DateOnly, long> GetDailyPowerSaverMinutes(string userId, DateOnly startDate, DateOnly endDate)
        {
            var dict = new Dictionary<DateOnly, long>();
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT q.quest_date, COALESCE(SUM(COALESCE(p.tracked_minutes, q.current_progress)), 0)
                FROM DAILY_QUESTS q
                LEFT JOIN POWER_SAVER_PROGRESS p ON p.quest_id = q.quest_id
                WHERE q.user_id = $user AND q.quest_type = 'PowerSaver'
                  AND q.quest_date BETWEEN $start AND $end
                GROUP BY q.quest_date;";
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$start", startDate.ToString(DateFormat, CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$end", endDate.ToString(DateFormat, CultureInfo.InvariantCulture));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var d = DateOnly.ParseExact(reader.GetString(0), DateFormat, CultureInfo.InvariantCulture);
                dict[d] = reader.GetInt64(1);
            }
            return dict;
        }

        private static void UpdatePetStateInTransaction(SqliteConnection connection, SqliteTransaction tx, string userId, DateOnly date)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT COALESCE(SUM(reward_coin), 0)
                FROM DAILY_QUESTS
                WHERE user_id = $user AND quest_date = $date AND is_claimed = 1;";
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
            long coinsToday = (long)cmd.ExecuteScalar()!;

            int happiness = (int)Math.Clamp(coinsToday * 3, 0, 100);
            string state = coinsToday >= 30 ? "Segar" : coinsToday >= 10 ? "Normal" : "Lemas";

            cmd.CommandText = @"
                INSERT INTO ECO_PETS (pet_id, user_id, happiness_level, current_state)
                VALUES ($pid, $user, $happy, $state)
                ON CONFLICT(user_id) DO UPDATE SET
                    happiness_level = excluded.happiness_level,
                    current_state = excluded.current_state;";
            cmd.Parameters.AddWithValue("$pid", "pet_" + userId);
            cmd.Parameters.AddWithValue("$happy", happiness);
            cmd.Parameters.AddWithValue("$state", state);
            cmd.ExecuteNonQuery();
        }

        private static void AddPowerSaverParameters(SqliteCommand cmd, string userId, string questId, DateOnly date)
        {
            cmd.Parameters.AddWithValue("$quest", questId);
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
        }

        /// <summary>Sessions of <paramref name="userId"/> with start_time in [from, to) ordered by start_time; open sessions (end_time NULL) included.</summary>
        public IReadOnlyList<PowerSession> GetPowerSessions(string userId, DateTime fromInclusive, DateTime toExclusive)
        {
            var result = new List<PowerSession>();

            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT session_id, user_id, scheme_id, start_time, end_time, watt_saved
                                FROM POWER_SESSIONS
                                WHERE user_id = $uid AND start_time >= $from AND start_time < $to
                                ORDER BY start_time ASC, rowid ASC;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$from", FormatDateTime(fromInclusive));
            cmd.Parameters.AddWithValue("$to", FormatDateTime(toExclusive));
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

        /// <summary>USERS.total_ecokoin of <paramref name="userId"/>; 0 if the user does not exist.</summary>
        public int GetTotalEcoKoin(string userId)
        {
            using var connection = OpenConnection();
            return ReadTotalEcoKoin(connection, null, userId);
        }

        /// <summary>Sum of reward_coin of the user's claimed quests whose quest_date is <paramref name="date"/> (0 if none).</summary>
        public int GetKoinEarnedOn(string userId, DateOnly date)
        {
            using var connection = OpenConnection();
            return ReadKoinEarnedOn(connection, null, userId, date);
        }

        /// <summary>
        /// Claimed koin per calendar day: exactly one entry per day in [from, to] (inclusive), ascending, 0 where nothing
        /// was claimed. An inverted range yields an empty list.
        /// </summary>
        public IReadOnlyList<(DateOnly Date, int Koin)> GetKoinEarnedPerDay(string userId, DateOnly fromInclusive, DateOnly toInclusive)
        {
            var result = new List<(DateOnly Date, int Koin)>();
            if (toInclusive < fromInclusive)
                return result;

            var byDay = new Dictionary<DateOnly, int>();
            using (var connection = OpenConnection())
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT quest_date, SUM(reward_coin) FROM DAILY_QUESTS
                                    WHERE user_id = $uid AND is_claimed = 1
                                      AND quest_date >= $from AND quest_date <= $to
                                    GROUP BY quest_date;";
                cmd.Parameters.AddWithValue("$uid", userId);
                cmd.Parameters.AddWithValue("$from", fromInclusive.ToString(DateFormat, CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$to", toInclusive.ToString(DateFormat, CultureInfo.InvariantCulture));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    byDay[DateOnly.ParseExact(reader.GetString(0), DateFormat, CultureInfo.InvariantCulture)] =
                        (int)reader.GetInt64(1);
                }
            }

            for (var day = fromInclusive; day <= toInclusive; day = day.AddDays(1))
                result.Add((day, byDay.GetValueOrDefault(day)));
            return result;
        }

        /// <summary>
        /// Atomically claims a quest of ANY type: only if it belongs to the user, has quest_date <paramref name="date"/>,
        /// is unclaimed and current_progress &gt;= target_value. Adds reward_coin to USERS.total_ecokoin in the same
        /// transaction. Returns true only when this call claimed it.
        /// </summary>
        public bool TryClaimQuest(string userId, string questId, DateOnly date)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                UPDATE DAILY_QUESTS SET is_claimed = 1
                WHERE quest_id = $quest AND user_id = $user AND quest_date = $date
                  AND is_claimed = 0 AND current_progress >= target_value
                RETURNING reward_coin;";
            AddPowerSaverParameters(cmd, userId, questId, date);
            if (cmd.ExecuteScalar() is not long reward)
                return false;

            cmd.CommandText = "UPDATE USERS SET total_ecokoin = total_ecokoin + $reward WHERE user_id = $user;";
            cmd.Parameters.AddWithValue("$reward", reward);
            cmd.ExecuteNonQuery();
            tx.Commit();
            return true;
        }

        /// <summary>
        /// Adds <paramref name="amount"/> to the user's Sleep or WeatherAdaptive quest of <paramref name="date"/>, clamped to
        /// target_value, only while it is unclaimed. Returns the new current_progress, or -1 if the quest does not exist or is
        /// already claimed. PowerSaver progress is owned by <see cref="RecordPowerSaverMinute"/>.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="type"/> is PowerSaver (or not a valid quest type).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive.</exception>
        public int AddQuestProgress(string userId, QuestType type, DateOnly date, int amount)
        {
            if (type == QuestType.PowerSaver)
                throw new ArgumentException("PowerSaver progress is recorded with RecordPowerSaverMinute.", nameof(type));
            if (type != QuestType.Sleep && type != QuestType.WeatherAdaptive)
                throw new ArgumentException($"Unsupported quest type '{type}'.", nameof(type));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");

            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE DAILY_QUESTS
                SET current_progress = MIN(current_progress + $amount, target_value)
                WHERE user_id = $user AND quest_type = $type AND quest_date = $date AND is_claimed = 0
                RETURNING current_progress;";
            cmd.Parameters.AddWithValue("$amount", amount);
            cmd.Parameters.AddWithValue("$user", userId);
            cmd.Parameters.AddWithValue("$type", type.ToString());
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
            return cmd.ExecuteScalar() is long progress ? (int)progress : -1;
        }

        /// <summary>
        /// Loads the user's pet (creating it if missing), refreshes it with <c>UpdateState(GetKoinEarnedOn(userId, today))</c>,
        /// persists happiness_level + current_state and returns it.
        /// </summary>
        public EcoPet GetOrCreatePet(string userId, DateOnly today)
        {
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT OR IGNORE INTO ECO_PETS (pet_id, user_id, happiness_level, current_state)
                                    VALUES ($pid, $uid, 0, 'Lemas');";
                cmd.Parameters.AddWithValue("$pid", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("$uid", userId);
                cmd.ExecuteNonQuery();
            }

            EcoPet pet;
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT pet_id, happiness_level, current_state FROM ECO_PETS WHERE user_id = $uid;";
                cmd.Parameters.AddWithValue("$uid", userId);
                using var reader = cmd.ExecuteReader();
                reader.Read();
                pet = new EcoPet(reader.GetString(0), userId, reader.GetInt32(1), Enum.Parse<PetState>(reader.GetString(2)));
            }

            pet.UpdateState(ReadKoinEarnedOn(connection, tx, userId, today));

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE ECO_PETS SET happiness_level = $happy, current_state = $state WHERE pet_id = $pid;";
                cmd.Parameters.AddWithValue("$happy", pet.HappinessLevel);
                cmd.Parameters.AddWithValue("$state", pet.CurrentState.ToString());
                cmd.Parameters.AddWithValue("$pid", pet.PetId);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return pet;
        }

        private static int ReadTotalEcoKoin(SqliteConnection connection, SqliteTransaction? tx, string userId)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT total_ecokoin FROM USERS WHERE user_id = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);
            return cmd.ExecuteScalar() is long total ? (int)total : 0;
        }

        private static int ReadKoinEarnedOn(SqliteConnection connection, SqliteTransaction? tx, string userId, DateOnly date)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"SELECT COALESCE(SUM(reward_coin), 0) FROM DAILY_QUESTS
                                WHERE user_id = $uid AND quest_date = $date AND is_claimed = 1;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$date", date.ToString(DateFormat, CultureInfo.InvariantCulture));
            return (int)Convert.ToInt64(cmd.ExecuteScalar());
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
