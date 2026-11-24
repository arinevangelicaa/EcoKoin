-- EcoKoin database schema v2 (PostgreSQL)
-- Base: docs/erd-hw.pdf (Modul 4). Changes vs v1: see docs/04-database.md
-- Safe to run repeatedly (IF NOT EXISTS). Objects are intentionally NOT schema-qualified: they are created
-- in the first schema of the connection's search_path (public by default).

CREATE TABLE IF NOT EXISTS USERS (
    user_id       TEXT PRIMARY KEY,
    username      TEXT NOT NULL,
    display_name  TEXT,
    password_hash TEXT NOT NULL,
    total_ecokoin INTEGER NOT NULL DEFAULT 0 CHECK (total_ecokoin >= 0),
    join_date     TIMESTAMP NOT NULL,
    CONSTRAINT uq_users_username UNIQUE (username)
);

-- seq keeps insertion order of quests (SQLite had rowid).
CREATE TABLE IF NOT EXISTS DAILY_QUESTS (
    quest_id         TEXT PRIMARY KEY,
    seq              BIGINT GENERATED ALWAYS AS IDENTITY,
    user_id          TEXT NOT NULL REFERENCES USERS(user_id),
    quest_type       TEXT NOT NULL CHECK (quest_type IN ('Sleep','PowerSaver','WeatherAdaptive')),
    description      TEXT NOT NULL,
    target_value     INTEGER NOT NULL CHECK (target_value > 0),
    current_progress INTEGER NOT NULL DEFAULT 0,
    reward_coin      INTEGER NOT NULL CHECK (reward_coin > 0),
    is_claimed       BOOLEAN NOT NULL DEFAULT FALSE,
    quest_date       DATE NOT NULL
);

CREATE TABLE IF NOT EXISTS ECO_PETS (
    pet_id          TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL UNIQUE REFERENCES USERS(user_id),
    happiness_level INTEGER NOT NULL DEFAULT 0 CHECK (happiness_level BETWEEN 0 AND 100),
    current_state   TEXT NOT NULL CHECK (current_state IN ('Segar','Normal','Lemas'))
);

CREATE TABLE IF NOT EXISTS POWER_SCHEMES (
    scheme_id           TEXT PRIMARY KEY,
    scheme_name         TEXT NOT NULL,
    cpu_max_performance INTEGER NOT NULL CHECK (cpu_max_performance BETWEEN 0 AND 100)
);

CREATE TABLE IF NOT EXISTS POWER_SESSIONS (
    session_id TEXT PRIMARY KEY,
    user_id    TEXT NOT NULL REFERENCES USERS(user_id),
    scheme_id  TEXT NOT NULL REFERENCES POWER_SCHEMES(scheme_id),
    start_time TIMESTAMP NOT NULL,
    end_time   TIMESTAMP,
    watt_saved DOUBLE PRECISION NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_sessions_user_start ON POWER_SESSIONS(user_id, start_time);
CREATE INDEX IF NOT EXISTS idx_quests_user_date    ON DAILY_QUESTS(user_id, quest_date);

CREATE TABLE IF NOT EXISTS POWER_SAVER_PROGRESS (
    quest_id TEXT PRIMARY KEY REFERENCES DAILY_QUESTS(quest_id) ON DELETE CASCADE,
    last_sample_minute BIGINT NOT NULL,
    tracked_minutes BIGINT
);
