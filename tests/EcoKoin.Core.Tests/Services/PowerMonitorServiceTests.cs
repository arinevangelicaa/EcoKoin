using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

public class PowerMonitorServiceTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 9, 0, 0);

    private static (PowerMonitorService svc, FakeIdleTimeProvider idle, FakePowerCommandRunner runner) Create(PowerOptions? options = null)
    {
        var idle = new FakeIdleTimeProvider();
        var runner = new FakePowerCommandRunner();
        return (new PowerMonitorService(idle, runner, new FakeClock(T0), options), idle, runner);
    }

    [Fact]
    public void DetectIdleState_BelowThreshold_NotIdle()
    {
        var (svc, idle, _) = Create();
        idle.Idle = TimeSpan.FromMinutes(4.99);

        Assert.False(svc.DetectIdleState());
        Assert.False(svc.IsIdle);
    }

    [Fact]
    public void DetectIdleState_AtThreshold_IsIdle()
    {
        var (svc, idle, _) = Create();
        idle.Idle = TimeSpan.FromMinutes(5);

        Assert.True(svc.DetectIdleState());
        Assert.True(svc.IsIdle);
    }

    [Fact]
    public void DetectIdleState_ReturnsToActive()
    {
        var (svc, idle, _) = Create();
        idle.Idle = TimeSpan.FromMinutes(10);
        svc.DetectIdleState();
        idle.Idle = TimeSpan.FromSeconds(1);

        Assert.False(svc.DetectIdleState());
        Assert.False(svc.IsIdle);
    }

    [Fact]
    public void DetectIdleState_UsesConfiguredThreshold()
    {
        var (svc, idle, _) = Create(new PowerOptions { IdleThresholdMinutes = 1 });
        idle.Idle = TimeSpan.FromMinutes(1);

        Assert.Equal(1, svc.IdleThresholdMinutes);
        Assert.True(svc.DetectIdleState());
    }

    [Fact]
    public void CalculateWattSaved_NoSchemeNotIdle_IsZero()
    {
        var (svc, _, _) = Create();
        Assert.Equal(0, svc.CalculateWattSaved());
    }

    [Fact]
    public void CalculateWattSaved_PowerSaverScheme()
    {
        var (svc, _, _) = Create();
        svc.ApplyPowerScheme(new PowerScheme("s", "Power Saver", 50));

        Assert.Equal(65 * 50 / 100.0, svc.CalculateWattSaved(), 6); // 32.5 W
    }

    [Fact]
    public void CalculateWattSaved_BalancedScheme()
    {
        var (svc, _, _) = Create();
        svc.ApplyPowerScheme(new PowerScheme("b", "Balanced", 80));

        Assert.Equal(13, svc.CalculateWattSaved(), 6);
    }

    [Fact]
    public void CalculateWattSaved_IdleAddsSleepSaving()
    {
        var (svc, idle, _) = Create();
        svc.ApplyPowerScheme(new PowerScheme("s", "Power Saver", 50));
        idle.Idle = TimeSpan.FromMinutes(6);
        svc.DetectIdleState();

        Assert.Equal(32.5 + 65 * 0.9, svc.CalculateWattSaved(), 6);
    }

    [Fact]
    public void CalculateWattSaved_IdleWithoutScheme_OnlySleepSaving()
    {
        var (svc, idle, _) = Create();
        idle.Idle = TimeSpan.FromMinutes(6);
        svc.DetectIdleState();

        Assert.Equal(65 * 0.9, svc.CalculateWattSaved(), 6);
    }

    [Fact]
    public void CalculateWattSaved_UsesBaselineOption()
    {
        var (svc, _, _) = Create(new PowerOptions { BaselineWatt = 100 });
        svc.ApplyPowerScheme(new PowerScheme("s", "Power Saver", 50));

        Assert.Equal(50, svc.CalculateWattSaved(), 6);
    }

    [Fact]
    public void CalculateWattSaved_FullPerformance_IsZero()
    {
        var (svc, _, _) = Create();
        svc.ApplyPowerScheme(new PowerScheme("hp", "High Performance", 100));

        Assert.Equal(0, svc.CalculateWattSaved());
    }

    [Fact]
    public void ApplyPowerScheme_ActivatesAndSetsCurrentScheme()
    {
        var (svc, _, runner) = Create();
        var scheme = new PowerScheme("s", "Power Saver", 50);

        svc.ApplyPowerScheme(scheme);

        Assert.Same(scheme, svc.CurrentScheme);
        Assert.Equal(3, runner.Calls.Count);
    }

    [Fact]
    public void ApplyPowerScheme_Failure_ThrowsAndKeepsPreviousScheme()
    {
        var (svc, _, runner) = Create();
        var first = new PowerScheme("b", "Balanced", 80);
        svc.ApplyPowerScheme(first);
        runner.Results[runner.Calls.Count] = new CommandResult(1, "denied");

        Assert.Throws<InvalidOperationException>(() => svc.ApplyPowerScheme(new PowerScheme("s", "Power Saver", 50)));
        Assert.Same(first, svc.CurrentScheme);
    }

    [Fact]
    public void ApplyPowerScheme_Null_Throws()
    {
        var (svc, _, _) = Create();
        Assert.Throws<ArgumentNullException>(() => svc.ApplyPowerScheme(null!));
    }
}
