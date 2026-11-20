using System;
using EcoKoin.Models;

namespace EcoKoin.Services
{
    public class PowerMonitorService
    {
        public bool IsIdle { get; set; }
        public double IdleThresholdMinutes { get; set; }
        public PowerScheme CurrentScheme { get; set; }

        public bool DetectIdleState()
        {
            throw new NotImplementedException();
        }

        public void ApplyPowerScheme(PowerScheme scheme)
        {
            throw new NotImplementedException();
        }

        public double CalculateWattSaved()
        {
            throw new NotImplementedException();
        }
    }
}
