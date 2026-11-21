using EcoKoin.Models;

namespace EcoKoin.Core.Tests.Models;

public class CarbonFinancialReportTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    private static PowerSession Closed(DateTime start, double hours, double watt) =>
        new(Guid.NewGuid().ToString(), "u1", "scheme", start, start.AddHours(hours), watt);

    private static DateTime At(DateOnly d, int hour = 9) => d.ToDateTime(new TimeOnly(hour, 0));

    [Fact]
    public void Totals_AreComputedFromClosedSessions()
    {
        // 50 W for 2 h = 100 Wh, 100 W for 1 h = 100 Wh -> 0.2 kWh
        var report = new CarbonFinancialReport(new[]
        {
            Closed(At(Today), 2, 50),
            Closed(At(Today, 14), 1, 100),
        }, Today);

        Assert.Equal(150, report.TotalWattSaved, 6);
        Assert.Equal(0.2, report.TotalKwhSaved, 6);
        Assert.Equal(0.2 * 0.87, report.TotalCo2Reduced, 6);
        Assert.Equal(0.2 * 1444.70, report.TotalRupiahSaved, 6);
    }

    [Fact]
    public void RunningSessions_AreIgnored()
    {
        var running = new PowerSession("s", "u1", "scheme", At(Today), null, 500);
        var report = new CarbonFinancialReport(new[] { running, Closed(At(Today, 12), 1, 100) }, Today);

        Assert.Equal(100, report.TotalWattSaved, 6);
        Assert.Equal(0.1, report.TotalKwhSaved, 6);
        Assert.Equal(0.1, report.GenerateWeeklyChart()[6].KwhSaved, 6);
    }

    [Fact]
    public void EmptySessions_GiveZeros()
    {
        var report = new CarbonFinancialReport(Array.Empty<PowerSession>(), Today);
        Assert.Equal(0, report.TotalWattSaved);
        Assert.Equal(0, report.TotalKwhSaved);
        Assert.Equal(0, report.TotalCo2Reduced);
        Assert.Equal(0, report.TotalRupiahSaved);
        Assert.All(report.GenerateWeeklyChart(), p => Assert.Equal(0, p.KwhSaved));
    }

    [Fact]
    public void CustomOptions_AreUsed()
    {
        var options = new ReportOptions { EmissionFactorKgPerKwh = 0.5, TariffRupiahPerKwh = 1000 };
        var report = new CarbonFinancialReport(new[] { Closed(At(Today), 10, 100) }, Today, options);

        Assert.Equal(1.0, report.TotalKwhSaved, 6);
        Assert.Equal(0.5, report.TotalCo2Reduced, 6);
        Assert.Equal(1000, report.TotalRupiahSaved, 6);
    }

    [Theory]
    [InlineData(1000, 0.87, 1444.70)]
    [InlineData(500, 0.435, 722.35)]
    [InlineData(0, 0, 0)]
    public void CalculateCo2AndRupiah_ConvertWattHoursToKwh(double wh, double co2, double rupiah)
    {
        var report = new CarbonFinancialReport(Array.Empty<PowerSession>(), Today);
        Assert.Equal(co2, report.CalculateCo2(wh), 6);
        Assert.Equal(rupiah, report.CalculateRupiah(wh), 6);
    }

    [Fact]
    public void CalculateCo2AndRupiah_NegativeInput_ReturnsZero()
    {
        var report = new CarbonFinancialReport(Array.Empty<PowerSession>(), Today);
        Assert.Equal(0, report.CalculateCo2(-100));
        Assert.Equal(0, report.CalculateRupiah(-100));
    }

    [Fact]
    public void WeeklyChart_HasSevenPointsOldestToNewestEndingToday()
    {
        var chart = new CarbonFinancialReport(Array.Empty<PowerSession>(), Today).GenerateWeeklyChart();

        Assert.Equal(7, chart.Count);
        Assert.Equal(Today.AddDays(-6), chart[0].Date);
        Assert.Equal(Today, chart[6].Date);
        for (int i = 1; i < chart.Count; i++)
            Assert.Equal(chart[i - 1].Date.AddDays(1), chart[i].Date);
    }

    [Fact]
    public void WeeklyChart_BucketsByStartDateAndZeroFillsMissingDays()
    {
        var report = new CarbonFinancialReport(new[]
        {
            Closed(At(Today.AddDays(-6)), 1, 100),      // oldest day: 0.1 kWh
            Closed(At(Today.AddDays(-6), 15), 1, 100),  // same day: +0.1 kWh
            Closed(At(Today.AddDays(-2)), 2, 50),       // 0.1 kWh
            Closed(At(Today), 1, 200),                  // 0.2 kWh
            Closed(At(Today.AddDays(-7)), 1, 999),      // outside window
            Closed(At(Today.AddDays(1)), 1, 999),       // future, outside window
        }, Today);

        var chart = report.GenerateWeeklyChart();

        Assert.Equal(0.2, chart[0].KwhSaved, 6);
        Assert.Equal(0, chart[1].KwhSaved);
        Assert.Equal(0, chart[2].KwhSaved);
        Assert.Equal(0, chart[3].KwhSaved);
        Assert.Equal(0.1, chart[4].KwhSaved, 6);
        Assert.Equal(0, chart[5].KwhSaved);
        Assert.Equal(0.2, chart[6].KwhSaved, 6);
    }

    [Fact]
    public void NullSessions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CarbonFinancialReport(null!, Today));
    }
}
