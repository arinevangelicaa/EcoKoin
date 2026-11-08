# EcoKoin Database — Bukti Pembuatan

Database dibuat dari `Database/schema.sql`, hasil jadi file `Database/ecokoin.db` (SQLite).

Cara buat ulang:
```bash
sqlite3 Database/ecokoin.db < Database/schema.sql
```

## Tabel yang terbentuk

```
sqlite> .tables
DAILY_QUESTS    ECO_PETS        POWER_SCHEMES   POWER_SESSIONS  USERS
```

## Struktur tiap tabel (`.schema`)

```sql
CREATE TABLE USERS (
    user_id       TEXT PRIMARY KEY,
    username      TEXT NOT NULL UNIQUE,
    total_ecokoin INTEGER NOT NULL DEFAULT 0,
    join_date     TEXT NOT NULL
);
CREATE TABLE DAILY_QUESTS (
    quest_id        TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL,
    description     TEXT NOT NULL,
    target_value    INTEGER NOT NULL,
    current_progress INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id)
);
CREATE TABLE ECO_PETS (
    pet_id          TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL UNIQUE,
    happiness_level INTEGER NOT NULL DEFAULT 0,
    current_state   TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id)
);
CREATE TABLE POWER_SCHEMES (
    scheme_id           TEXT PRIMARY KEY,
    scheme_name         TEXT NOT NULL,
    cpu_max_performance INTEGER NOT NULL
);
CREATE TABLE POWER_SESSIONS (
    session_id  TEXT PRIMARY KEY,
    user_id     TEXT NOT NULL,
    scheme_id   TEXT NOT NULL,
    start_time  TEXT NOT NULL,
    end_time    TEXT,
    watt_saved  REAL NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES USERS(user_id),
    FOREIGN KEY (scheme_id) REFERENCES POWER_SCHEMES(scheme_id)
);
```

## Isi tabel (sample data)

**USERS**
| user_id | username | total_ecokoin | join_date |
|---|---|---|---|
| u1 | adnan | 120 | 2026-01-10 08:00:00 |
| u2 | arin | 75 | 2026-02-01 09:30:00 |

**DAILY_QUESTS**
| quest_id | user_id | description | target_value | current_progress |
|---|---|---|---|---|
| q1 | u1 | Aktifkan mode sleep 3x hari ini | 3 | 1 |
| q2 | u2 | Hemat 50 watt hari ini | 50 | 20 |

**ECO_PETS**
| pet_id | user_id | happiness_level | current_state |
|---|---|---|---|
| p1 | u1 | 80 | Segar |
| p2 | u2 | 40 | Normal |

**POWER_SCHEMES**
| scheme_id | scheme_name | cpu_max_performance |
|---|---|---|
| s1 | Power Saver | 50 |
| s2 | Balanced | 80 |

**POWER_SESSIONS**
| session_id | user_id | scheme_id | start_time | end_time | watt_saved |
|---|---|---|---|---|---|
| ps1 | u1 | s1 | 2026-03-01 08:00:00 | 2026-03-01 10:00:00 | 15.5 |
| ps2 | u2 | s2 | 2026-03-01 09:00:00 | 2026-03-01 11:00:00 | 8.2 |

Relasi FK teruji: `DAILY_QUESTS.user_id`, `ECO_PETS.user_id` (unik), `POWER_SESSIONS.user_id`, `POWER_SESSIONS.scheme_id` semua mengacu ke baris valid di `USERS` / `POWER_SCHEMES`.
