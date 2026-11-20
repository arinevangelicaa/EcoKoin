using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using EcoKoin.Models;

namespace EcoKoin.Services
{
    public class SqliteRepository
    {
        private readonly string _connectionString;

        public SqliteRepository(string connectionString = "Data Source=ecokoin.db")
        {
            _connectionString = connectionString;
        }

        public void InitializeDatabase()
        {
            string schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");
            string schemaSql = File.ReadAllText(schemaPath);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = schemaSql;
            command.ExecuteNonQuery();
        }

        public void SaveUser(User user)
        {
            throw new NotImplementedException();
        }

        public User GetUserByUsername(string u)
        {
            throw new NotImplementedException();
        }

        public void SavePowerSession(PowerSession session)
        {
            throw new NotImplementedException();
        }

        public void SaveQuestProgress(DailyQuest quest)
        {
            throw new NotImplementedException();
        }

        public List<PowerSession> GetWeeklyReportData()
        {
            throw new NotImplementedException();
        }

        public void UpdateUserProfile(User user)
        {
            throw new NotImplementedException();
        }
    }
}
