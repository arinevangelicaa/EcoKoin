using EcoKoin.ViewModels;

namespace EcoKoin.Core.Tests.ViewModels;

public class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RunsDelegateWithParameter()
    {
        object? seen = null;
        var cmd = new AsyncRelayCommand(p => { seen = p; return Task.CompletedTask; });

        await cmd.ExecuteAsync("x");

        Assert.Equal("x", seen);
        Assert.False(cmd.IsRunning);
    }

    [Fact]
    public async Task WhileRunning_CannotExecuteAgain_AndStateChangesAreRaised()
    {
        var gate = new TaskCompletionSource();
        int runs = 0;
        var cmd = new AsyncRelayCommand(async _ => { runs++; await gate.Task; });
        int raised = 0;
        cmd.CanExecuteChanged += (_, _) => raised++;

        var first = cmd.ExecuteAsync(null);
        Assert.True(cmd.IsRunning);
        Assert.False(cmd.CanExecute(null));
        await cmd.ExecuteAsync(null); // ignored while running
        Assert.Equal(1, runs);

        gate.SetResult();
        await first;

        Assert.False(cmd.IsRunning);
        Assert.True(cmd.CanExecute(null));
        Assert.Equal(2, raised); // started + finished
    }

    [Fact]
    public async Task CanExecutePredicate_IsRespected()
    {
        int runs = 0;
        var cmd = new AsyncRelayCommand(_ => { runs++; return Task.CompletedTask; }, p => p is "ok");

        await cmd.ExecuteAsync("no");
        await cmd.ExecuteAsync("ok");

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesExceptionsAndResetsRunningState()
    {
        var cmd = new AsyncRelayCommand(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => cmd.ExecuteAsync(null));

        Assert.False(cmd.IsRunning);
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public async Task Execute_WithOnError_ReportsInsteadOfThrowing()
    {
        var reported = new TaskCompletionSource<Exception>();
        var cmd = new AsyncRelayCommand(_ => throw new InvalidOperationException("boom"), onError: ex => reported.SetResult(ex));

        cmd.Execute(null);

        var ex = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("boom", ex.Message);
    }
}
