using RoslynGate.Cli;

namespace RoslynGate.E2E.Tests;

public sealed class McpFixture : IAsyncLifetime, IDisposable
{
    private McpSession? _session;

    public McpSession Session => Assert.IsType<McpSession>(_session);

    public async Task InitializeAsync()
    {
        _session = await McpSession.StartAsync(
            TestPaths.RepositoryRoot,
            CliNames.Solution,
            TestPaths.SampleSolution,
            CliNames.NoPrewarm);
    }

    public async Task DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }

    public void Dispose()
    {
        _session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _session = null;
    }
}
