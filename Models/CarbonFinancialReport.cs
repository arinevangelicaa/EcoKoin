using System;

namespace EcoKoin.Models
{
    public class CarbonFinancialReport
    {
        public double TotalWattSaved { get; set; }
        public double TotalCo2Reduced { get; set; }
        public double TotalRupiahSaved { get; set; }

        public object GenerateWeeklyChart()
        {
            throw new NotImplementedException();
        }

        public double CalculateCo2(double watt)
        {
            throw new NotImplementedException();
        }

        public double CalculateRupiah(double watt)
        {
            throw new NotImplementedException();
        }
    }
}
