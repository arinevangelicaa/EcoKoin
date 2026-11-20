using System;

namespace EcoKoin.Models
{
    public class PowerScheme
    {
        public string SchemeId { get; set; }
        public string SchemeName { get; set; }
        public int CpuMaxPerformance { get; set; }

        public void Activate()
        {
            throw new NotImplementedException();
        }
    }
}
