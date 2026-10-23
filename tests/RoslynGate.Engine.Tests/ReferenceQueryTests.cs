using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

[Collection(WorkspaceTestGroup.Name)]
public sealed class ReferenceQueryTests : IAsyncLifetime, IDisposable
{
    private Workspace? _workspace;
    private WorkspaceSnapshot? _snapshot;

    public async Task InitializeAsync()
    {
        _workspace = new Workspace(TestPaths.SampleSolution);
        _snapshot = await _workspace.GetAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _workspace?.Dispose();
        _workspace = null;
    }

    [Fact]
    public async Task FindsSemanticReferences()
    {
        var target = new SymbolTarget(
            null,
            "SampleSdk/Widget.cs",
            FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)"));

        var result = await GetAsync(target);

        Assert.False(result.Ambiguous);
        Assert.NotNull(result.Symbol);
        Assert.True(result.Total >= 2);
        Assert.All(result.References, reference => Assert.Equal("SampleSdk/DoorController.cs", reference.Location.File));
        Assert.Contains(result.References, reference => reference.Kind == "call");
    }

    [Fact]
    public async Task IncludesDeclarationsWhenRequested()
    {
        var target = new SymbolTarget(
            null,
            "SampleSdk/Widget.cs",
            FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)"));

        var result = await GetAsync(target, includeDeclarations: true);

        Assert.Contains(result.References, reference => reference.Kind == "declaration");
    }

    [Fact]
    public async Task PaginatesReferencesDeterministically()
    {
        var target = new SymbolTarget(
            null,
            "SampleSdk/Widget.cs",
            FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)"));

        var first = await GetAsync(target, limit: 1);
        var second = await GetAsync(target, offset: 1, limit: 1);

        Assert.Single(first.References);
        Assert.Single(second.References);
        Assert.True(first.Truncated);
        Assert.NotEqual(first.References[0].Location, second.References[0].Location);
    }

    [Fact]
    public async Task AmbiguousTargetReturnsCandidates()
    {
        var result = await GetAsync(new SymbolTarget("SampleSdk.Widget.Open", null, null, null));

        Assert.True(result.Ambiguous);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Empty(result.References);
    }

    [Fact]
    public async Task UnknownTargetReturnsEmptyReferences()
    {
        var result = await GetAsync(new SymbolTarget("SampleSdk.Missing", null, null, null));

        Assert.Null(result.Symbol);
        Assert.False(result.Ambiguous);
        Assert.Empty(result.References);
    }

    private async Task<ReferencesResult> GetAsync(
        SymbolTarget target,
        bool includeDeclarations = false,
        int offset = 0,
        int limit = QueryLimits.DefaultReferenceLimit)
    {
        var snapshot = Assert.IsType<WorkspaceSnapshot>(_snapshot);
        return await ReferenceQuery.GetAsync(
            snapshot,
            target,
            includeDeclarations,
            offset,
            limit,
            CancellationToken.None);
    }
}
