-- EcoKoin seed data (PostgreSQL). Idempotent.
INSERT INTO POWER_SCHEMES (scheme_id, scheme_name, cpu_max_performance) VALUES
    ('scheme-power-saver', 'Power Saver', 50),
    ('scheme-balanced',    'Balanced',    80)
ON CONFLICT (scheme_id) DO NOTHING;
