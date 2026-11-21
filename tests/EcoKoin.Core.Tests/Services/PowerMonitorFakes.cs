using System.Net;
using EcoKoin.Services;

namespace EcoKoin.Core.Tests.Services;

internal sealed class FakeClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakeIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan Idle { get; set; }
    public TimeSpan GetIdleTime() => Idle;
}

internal sealed class FakePowerCommandRunner : IPowerCommandRunner
{
    public List<(string File, string Args)> Calls { get; } = new();
    /// <summary>Exit code per call index (default 0).</summary>
    public Dictionary<int, CommandResult> Results { get; } = new();

    public CommandResult Run(string fileName, string arguments)
    {
        int index = Calls.Count;
        Calls.Add((fileName, arguments));
        return Results.TryGetValue(index, out var r) ? r : new CommandResult(0, "");
    }
}

internal sealed class StubHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK);
    public List<Uri> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(Respond(request));
    }
}
