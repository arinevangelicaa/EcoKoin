using System;
using System.Collections.Generic;

namespace EcoKoin.Models
{
    public class User
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public int TotalEcoKoin { get; set; }
        public DateTime JoinDate { get; set; }

        // User 1 -- * DailyQuest
        public List<DailyQuest> Quests { get; set; } = new List<DailyQuest>();

        // User 1 -- 1 EcoPet
        public EcoPet Pet { get; set; }

        public void AddEcoKoin(int amount)
        {
            TotalEcoKoin += amount;
        }

        public int GetBalance()
        {
            return TotalEcoKoin;
        }

        public bool Login(string u, string p)
        {
            throw new NotImplementedException();
        }
    }
}
