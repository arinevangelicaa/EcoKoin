-- EcoKoin database schema
-- Source: docs/erd-hw.pdf (Modul 4 - Perancangan Basis Data)

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS USERS (
    user_id       TEXT PRIMARY KEY,
    username      TEXT NOT NULL UNIQUE,
    total_ecokoin INTEGER NOT NULL DEFAULT 0,
    join_date     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS DAILY_QUESTS (
    quest_id        TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL,
    description     TEXT NOT NULL,
    target_value    INTEGER NOT NULL,
    current_progress INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id)
);

CREATE TABLE IF NOT EXISTS ECO_PETS (
    pet_id          TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL UNIQUE,
    happiness_level INTEGER NOT NULL DEFAULT 0,
    current_state   TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id)
);

CREATE TABLE IF NOT EXISTS POWER_SCHEMES (
    scheme_id           TEXT PRIMARY KEY,
    scheme_name         TEXT NOT NULL,
    cpu_max_performance INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS POWER_SESSIONS (
    session_id  TEXT PRIMARY KEY,
    user_id     TEXT NOT NULL,
    scheme_id   TEXT NOT NULL,
    start_time  TEXT NOT NULL,
    end_time    TEXT,
    watt_saved  REAL NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id),
    FOREIGN KEY (scheme_id) REFERENCES POWER_SCHEMES(scheme_id)
);
