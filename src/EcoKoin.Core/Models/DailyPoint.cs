namespace EcoKoin.Models
{
    /// <summary>One point of the weekly chart: kWh saved on <see cref="Date"/>.</summary>
    public record DailyPoint(DateOnly Date, double KwhSaved);
}
