-- EcoKoin seed data. Idempotent.
INSERT OR IGNORE INTO POWER_SCHEMES (scheme_id, scheme_name, cpu_max_performance) VALUES ('scheme-power-saver', 'Power Saver', 50);
INSERT OR IGNORE INTO POWER_SCHEMES (scheme_id, scheme_name, cpu_max_performance) VALUES ('scheme-balanced', 'Balanced', 80);
