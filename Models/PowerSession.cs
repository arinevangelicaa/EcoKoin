using System;

namespace EcoKoin.Models
{
    public class PowerSession
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double WattSaved { get; set; }

        public void SaveToDatabase()
        {
            throw new NotImplementedException();
        }
    }
}
