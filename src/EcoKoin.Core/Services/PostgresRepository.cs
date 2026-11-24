using EcoKoin.Models;
using Npgsql;
using NpgsqlTypes;

namespace EcoKoin.Services
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="IAppRepository"/> (the production data layer).
    /// Tables use unquoted identifiers, so PostgreSQL stores them in lower case (users, daily_quests, ...).
    /// Timestamps are stored as <c>TIMESTAMP</c> (no time zone) and returned with <see cref="DateTimeKind.Unspecified"/>,
    /// i.e. exactly the wall-clock values the application passed in.
    /// Each call opens a pooled connection from one <see cref="NpgsqlDataSource"/>, so the repository is safe to use
    /// from several tasks at once. The connection string is never logged or included in exception messages.
    /// Credentials: the database user needs CREATE rights on its schema because <see cref="InitializeDatabaseAsync"/>
    /// creates the tables on first start.
    /// </summary>
    public sealed class PostgresRepository : IAppRepository, IAsyncDisposable, IDisposable
    {
        private const string SchemaResource = "EcoKoin.Database.schema.sql";
        private const string SeedResource = "EcoKoin.Database.seed.sql";
        private const string UsernameConstraint = "uq_users_username";
        private const string UniqueViolation = "23505";

        // Arbitrary constant: serializes concurrent InitializeDatabaseAsync calls (several app instances starting at once).
        private const long InitLockKey = 0x45636F4B6F696E; // "EcoKoin"

        private readonly NpgsqlDataSource _dataSource;

        public PostgresRepository(string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            _dataSource = NpgsqlDataSource.Create(connectionString);
        }

        public void Dispose() => _dataSource.Dispose();

        public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

        public async Task InitializeDatabaseAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            string schemaSql = ReadResource(SchemaResource);
            string seedSql = ReadResource(SeedResource);

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key);", connection, tx))
            {
                lockCmd.Parameters.AddWithValue("key", InitLockKey);
                await lockCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var schemaCmd = new NpgsqlCommand(schemaSql, connection, tx))
                await schemaCmd.ExecuteNonQueryAsync(ct);

            await using (var seedCmd = new NpgsqlCommand(seedSql, connection, tx))
                await seedCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
        }

        public async Task SaveUserAsync(User user, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync(ct);
                await using var tx = await connection.BeginTransactionAsync(ct);

                await using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO USERS (user_id, username, password_hash, total_ecokoin, join_date, display_name)
                    VALUES (@id, @username, @hash, @coins, @join, @displayName)
                    ON CONFLICT (user_id) DO UPDATE SET
                        username = EXCLUDED.username,
                        password_hash = EXCLUDED.password_hash,
                        total_ecokoin = EXCLUDED.total_ecokoin,
                        join_date = EXCLUDED.join_date,
                        display_name = COALESCE(EXCLUDED.display_name, USERS.display_name);", connection, tx))
                {
                    cmd.Parameters.AddWithValue("id", user.UserId);
                    cmd.Parameters.AddWithValue("username", user.Username);
                    cmd.Parameters.AddWithValue("hash", user.PasswordHash);
                    cmd.Parameters.AddWithValue("coins", user.TotalEcoKoin);
                    AddTimestamp(cmd, "join", user.JoinDate);
                    cmd.Parameters.Add(new NpgsqlParameter("displayName", NpgsqlDbType.Text) { Value = (object?)user.DisplayName ?? DBNull.Value });
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await UpsertPetAsync(connection, tx, user, ct);
                foreach (var quest in user.Quests)
                    await UpsertQuestAsync(connection, tx, quest, ct);

                await tx.CommitAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == UniqueViolation && ex.ConstraintName == UsernameConstraint)
            {
                throw new DuplicateUsernameException(user.Username, ex);
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(username);
            ct.ThrowIfCancellationRequested();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);

            User user;
            await using (var cmd = new NpgsqlCommand(@"
                SELECT user_id, username, password_hash, total_ecokoin, join_date, display_name
                FROM USERS WHERE username = @username;", connection))
            {
                cmd.Parameters.AddWithValue("username", username);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    return null;

                user = new User(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetDateTime(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5));
            }

            await using (var cmd = new NpgsqlCommand(@"
                SELECT pet_id, happiness_level, current_state
                FROM ECO_PETS WHERE user_id = @uid;", connection))
            {
                cmd.Parameters.AddWithValue("uid", user.UserId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    user.Pet = new EcoPet(
                        reader.GetString(0),
                        user.UserId,
                        reader.GetInt32(1),
                        Enum.Parse<PetState>(reader.GetString(2)));
                }
            }

            await using (var cmd = new NpgsqlCommand(@"
                SELECT quest_id, quest_type, description, target_value, reward_coin,
                       quest_date, current_progress, is_claimed
                FROM DAILY_QUESTS
                WHERE user_id = @uid
                  AND quest_date = (SELECT MAX(quest_date) FROM DAILY_QUESTS WHERE user_id = @uid)
                ORDER BY seq;", connection))
            {
                cmd.Parameters.AddWithValue("uid", user.UserId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    user.Quests.Add(ReadQuest(reader, user.UserId));
            }

            return user;
        }

        public async Task UpdateUserProfileAsync(User user, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            ct.ThrowIfCancellationRequested();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            await using (var cmd = new NpgsqlCommand("UPDATE USERS SET total_ecokoin = @coins WHERE user_id = @id;", connection, tx))
            {
                cmd.Parameters.AddWithValue("coins", user.TotalEcoKoin);
                cmd.Parameters.AddWithValue("id", user.UserId);
                if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                    throw new InvalidOperationException($"User '{user.UserId}' does not exist.");
            }

            await UpsertPetAsync(connection, tx, user, ct);
            foreach (var quest in user.Quests)
                await UpsertQuestAsync(connection, tx, quest, ct);

            await tx.CommitAsync(ct);
        }

        public async Task SavePowerSessionAsync(PowerSession session, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(session);
            ct.ThrowIfCancellationRequested();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO POWER_SESSIONS (session_id, user_id, scheme_id, start_time, end_time, watt_saved)
                VALUES (@id, @uid, @sid, @start, @end, @watt)
                ON CONFLICT (session_id) DO UPDATE SET
                    user_id = EXCLUDED.user_id,
                    scheme_id = EXCLUDED.scheme_id,
                    start_time = EXCLUDED.start_time,
                    end_time = EXCLUDED.end_time,
                    watt_saved = EXCLUDED.watt_saved;", connection);
            cmd.Parameters.AddWithValue("id", session.SessionId);
            cmd.Parameters.AddWithValue("uid", session.UserId);
            cmd.Parameters.AddWithValue("sid", session.SchemeId);
            AddTimestamp(cmd, "start", session.StartTime);
            AddTimestamp(cmd, "end", session.EndTime);
            cmd.Parameters.AddWithValue("watt", session.WattSaved);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveQuestProgressAsync(DailyQuest quest, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(quest);
            ct.ThrowIfCancellationRequested();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await UpsertQuestAsync(connection, null, quest, ct);
        }

        public async Task<IReadOnlyList<PowerSession>> GetWeeklyReportDataAsync(string userId, DateTime now, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(userId);
            ct.ThrowIfCancellationRequested();

            var result = new List<PowerSession>();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                SELECT session_id, user_id, scheme_id, start_time, end_time, watt_saved
                FROM POWER_SESSIONS
                WHERE user_id = @uid AND start_time >= @since
                ORDER BY start_time ASC, session_id ASC;", connection);
            cmd.Parameters.AddWithValue("uid", userId);
            AddTimestamp(cmd, "since", now.AddDays(-7));
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new PowerSession(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    reader.GetDouble(5)));
            }

            return result;
        }

        public async Task<IReadOnlyList<PowerScheme>> GetPowerSchemesAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var result = new List<PowerScheme>();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                SELECT scheme_id, scheme_name, cpu_max_performance
                FROM POWER_SCHEMES ORDER BY cpu_max_performance, scheme_id;", connection);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(new PowerScheme(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));

            return result;
        }

        public async Task<IReadOnlyList<DailyQuest>> EnsureDailyQuestsAsync(string userId, DateOnly date, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(userId);
            ct.ThrowIfCancellationRequested();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            // Lock the user row: concurrent callers for the same user queue up here, so the "none yet -> insert 3"
            // decision below cannot create duplicates. A missing user creates nothing.
            await using (var lockCmd = new NpgsqlCommand("SELECT 1 FROM USERS WHERE user_id = @uid FOR UPDATE;", connection, tx))
            {
                lockCmd.Parameters.AddWithValue("uid", userId);
                if (await lockCmd.ExecuteScalarAsync(ct) is null)
                    throw new InvalidOperationException($"User '{userId}' does not exist.");
            }

            long existing;
            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM DAILY_QUESTS WHERE user_id = @uid AND quest_date = @date;", connection, tx))
            {
                cmd.Parameters.AddWithValue("uid", userId);
                cmd.Parameters.Add(new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = date });
                existing = (long)(await cmd.ExecuteScalarAsync(ct))!;
            }

            if (existing == 0)
            {
                foreach (var quest in DailyQuest.CreateDefaultsFor(userId, date))
                    await UpsertQuestAsync(connection, tx, quest, ct);
            }

            var quests = new List<DailyQuest>();
            await using (var cmd = new NpgsqlCommand(@"
                SELECT quest_id, quest_type, description, target_value, reward_coin,
                       quest_date, current_progress, is_claimed
                FROM DAILY_QUESTS
                WHERE user_id = @uid AND quest_date = @date
                ORDER BY seq;", connection, tx))
            {
                cmd.Parameters.AddWithValue("uid", userId);
                cmd.Parameters.Add(new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = date });
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    quests.Add(ReadQuest(reader, userId));
            }

            await tx.CommitAsync(ct);
            return quests;
        }

        // ---- helpers ------------------------------------------------------------------------

        private static async Task UpsertPetAsync(NpgsqlConnection connection, NpgsqlTransaction tx, User user, CancellationToken ct)
        {
            if (user.Pet is null)
                return;

            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO ECO_PETS (pet_id, user_id, happiness_level, current_state)
                VALUES (@pid, @uid, @happy, @state)
                ON CONFLICT (pet_id) DO UPDATE SET
                    happiness_level = EXCLUDED.happiness_level,
                    current_state = EXCLUDED.current_state;", connection, tx);
            cmd.Parameters.AddWithValue("pid", user.Pet.PetId);
            cmd.Parameters.AddWithValue("uid", user.UserId);
            cmd.Parameters.AddWithValue("happy", user.Pet.HappinessLevel);
            cmd.Parameters.AddWithValue("state", user.Pet.CurrentState.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static async Task UpsertQuestAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, DailyQuest quest, CancellationToken ct)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO DAILY_QUESTS (quest_id, user_id, quest_type, description, target_value,
                                          current_progress, reward_coin, is_claimed, quest_date)
                VALUES (@id, @uid, @type, @desc, @target, @progress, @reward, @claimed, @date)
                ON CONFLICT (quest_id) DO UPDATE SET
                    current_progress = EXCLUDED.current_progress,
                    is_claimed = EXCLUDED.is_claimed;", connection, tx);
            cmd.Parameters.AddWithValue("id", quest.QuestId);
            cmd.Parameters.AddWithValue("uid", quest.UserId);
            cmd.Parameters.AddWithValue("type", quest.Type.ToString());
            cmd.Parameters.AddWithValue("desc", quest.Description);
            cmd.Parameters.AddWithValue("target", quest.TargetValue);
            cmd.Parameters.AddWithValue("progress", quest.CurrentProgress);
            cmd.Parameters.AddWithValue("reward", quest.RewardCoin);
            cmd.Parameters.AddWithValue("claimed", quest.IsClaimed);
            cmd.Parameters.Add(new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = quest.QuestDate });
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>Reads columns: quest_id, quest_type, description, target_value, reward_coin, quest_date, current_progress, is_claimed.</summary>
        private static DailyQuest ReadQuest(NpgsqlDataReader reader, string userId) =>
            DailyQuest.Create(
                Enum.Parse<QuestType>(reader.GetString(1)),
                reader.GetString(0),
                userId,
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetFieldValue<DateOnly>(5),
                reader.GetInt32(6),
                reader.GetBoolean(7));

        /// <summary>Binds a wall-clock timestamp to a TIMESTAMP (without time zone) column; null becomes SQL NULL.</summary>
        private static void AddTimestamp(NpgsqlCommand cmd, string name, DateTime? value)
        {
            cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Timestamp)
            {
                Value = value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified) : DBNull.Value
            });
        }

        private static string ReadResource(string name)
        {
            using Stream stream = typeof(PostgresRepository).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded resource '{name}' was not found in {typeof(PostgresRepository).Assembly.GetName().Name}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
