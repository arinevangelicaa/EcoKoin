namespace EcoKoin.Models
{
    public class ReportOptions
    {
        /// <summary>kg CO2 per kWh.</summary>
        public double EmissionFactorKgPerKwh { get; set; } = 0.87;

        /// <summary>Rupiah per kWh.</summary>
        public double TariffRupiahPerKwh { get; set; } = 1444.70;
    }
}
