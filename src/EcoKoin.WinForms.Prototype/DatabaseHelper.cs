using System;
using Npgsql;

namespace ecokoin7
{
    public static class DatabaseHelper
    {
        public static string Host = "localhost";
        public static int Port = 5432;
        public static string Database = "ecokoin_db";
        public static string Username = "postgres";

        // Set ECOKOIN_DB_PASSWORD in the environment instead of storing credentials in source.
        public static string Password => Environment.GetEnvironmentVariable("ECOKOIN_DB_PASSWORD") ?? string.Empty;

        public static string ConnectionString =>
            $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";

        // =====================================================================
        // OPERASI SELECT: Mengambil data pengguna dari tabel USERS dan ECO_PETS
        // =====================================================================
        public static bool LoadUserData(string username, out int totalKoin, out string petState)
        {
            totalKoin = 120;
            petState = "Normal";

            try
            {
                using (var conn = new NpgsqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT u.total_ecokoin, COALESCE(p.current_state, 'Normal') AS current_state
                        FROM USERS u
                        LEFT JOIN ECO_PETS p ON u.user_id = p.user_id
                        WHERE u.username = @user
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        // Parameterized query untuk keamanan
                        cmd.Parameters.AddWithValue("user", username);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                totalKoin = reader.GetInt32(0);
                                petState = reader.GetString(1);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback aman jika database belum aktif agar aplikasi tidak langsung force close
            }

            return false;
        }

        // =====================================================================
        // OPERASI UPDATE: Menambah koin pengguna di tabel USERS
        // =====================================================================
        public static bool TambahKoin(string username, int tambahanKoin)
        {
            try
            {
                using (var conn = new NpgsqlConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        UPDATE USERS 
                        SET total_ecokoin = total_ecokoin + @koin 
                        WHERE username = @user;";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("koin", tambahanKoin);
                        cmd.Parameters.AddWithValue("user", username);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
