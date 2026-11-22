-- EcoKoin database schema v2
-- Base: docs/erd-hw.pdf (Modul 4). Changes vs v1: see docs/04-database.md
-- Safe to run repeatedly (IF NOT EXISTS). Foreign keys must also be enabled per connection.

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS USERS (
    user_id       TEXT PRIMARY KEY,
    username      TEXT NOT NULL UNIQUE,
    display_name  TEXT,
    password_hash TEXT NOT NULL,
    total_ecokoin INTEGER NOT NULL DEFAULT 0 CHECK (total_ecokoin >= 0),
    join_date     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS DAILY_QUESTS (
    quest_id         TEXT PRIMARY KEY,
    user_id          TEXT NOT NULL,
    quest_type       TEXT NOT NULL CHECK (quest_type IN ('Sleep','PowerSaver','WeatherAdaptive')),
    description      TEXT NOT NULL,
    target_value     INTEGER NOT NULL CHECK (target_value > 0),
    current_progress INTEGER NOT NULL DEFAULT 0,
    reward_coin      INTEGER NOT NULL CHECK (reward_coin > 0),
    is_claimed       INTEGER NOT NULL DEFAULT 0 CHECK (is_claimed IN (0,1)),
    quest_date       TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id)
);

CREATE TABLE IF NOT EXISTS ECO_PETS (
    pet_id          TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL UNIQUE,
    happiness_level INTEGER NOT NULL DEFAULT 0 CHECK (happiness_level BETWEEN 0 AND 100),
    current_state   TEXT NOT NULL CHECK (current_state IN ('Segar','Normal','Lemas')),
    FOREIGN KEY (user_id) REFERENCES USERS(user_id)
);

CREATE TABLE IF NOT EXISTS POWER_SCHEMES (
    scheme_id           TEXT PRIMARY KEY,
    scheme_name         TEXT NOT NULL,
    cpu_max_performance INTEGER NOT NULL CHECK (cpu_max_performance BETWEEN 0 AND 100)
);

CREATE TABLE IF NOT EXISTS POWER_SESSIONS (
    session_id TEXT PRIMARY KEY,
    user_id    TEXT NOT NULL,
    scheme_id  TEXT NOT NULL,
    start_time TEXT NOT NULL,
    end_time   TEXT,
    watt_saved REAL NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id)   REFERENCES USERS(user_id),
    FOREIGN KEY (scheme_id) REFERENCES POWER_SCHEMES(scheme_id)
);

CREATE INDEX IF NOT EXISTS idx_sessions_user_start ON POWER_SESSIONS(user_id, start_time);
CREATE INDEX IF NOT EXISTS idx_quests_user_date    ON DAILY_QUESTS(user_id, quest_date);

CREATE TABLE IF NOT EXISTS POWER_SAVER_PROGRESS (
    quest_id TEXT PRIMARY KEY REFERENCES DAILY_QUESTS(quest_id) ON DELETE CASCADE,
    last_sample_minute INTEGER NOT NULL
);
