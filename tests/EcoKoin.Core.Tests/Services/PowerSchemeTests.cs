using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

public class PowerSchemeTests
{
    [Fact]
    public void Activate_RunsThreePowercfgCommandsInOrder()
    {
        var runner = new FakePowerCommandRunner();

        new PowerScheme("s", "Power Saver", 50).Activate(runner);

        Assert.Equal(3, runner.Calls.Count);
        Assert.All(runner.Calls, c => Assert.Equal("powercfg", c.File));
        Assert.Equal("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 50", runner.Calls[0].Args);
        Assert.Equal("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 50", runner.Calls[1].Args);
        Assert.Equal("/setactive SCHEME_CURRENT", runner.Calls[2].Args);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Activate_NonZeroExit_ThrowsWithOutputAndStops(int failingCall)
    {
        var runner = new FakePowerCommandRunner();
        runner.Results[failingCall] = new CommandResult(5, "Access is denied");

        var ex = Assert.Throws<InvalidOperationException>(() => new PowerScheme("s", "Power Saver", 50).Activate(runner));

        Assert.Contains("Access is denied", ex.Message);
        Assert.Equal(failingCall + 1, runner.Calls.Count);
    }
}
