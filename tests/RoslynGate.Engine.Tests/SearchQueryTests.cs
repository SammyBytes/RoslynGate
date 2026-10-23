using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

[Collection(WorkspaceTestGroup.Name)]
public sealed class SearchQueryTests : IAsyncLifetime, IDisposable
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
    public async Task RanksExactNamesFirst()
    {
        var result = await GetAsync("Widget");

        Assert.True(result.Total > 0);
        Assert.Equal("SampleSdk.Widget", result.Hits[0].QualifiedName);
        Assert.Contains(result.Hits, hit => hit.QualifiedName == "SampleSdk.SecureWidget");
    }

    [Fact]
    public async Task FiltersByKind()
    {
        var result = await GetAsync("Open", kind: "method");

        Assert.True(result.Total >= 3);
        Assert.All(result.Hits, hit => Assert.Equal("method", hit.Kind));
    }

    [Fact]
    public async Task FiltersByNamespace()
    {
        var result = await GetAsync("Open", namespaceFilter: "SampleSdk");
        var missing = await GetAsync("Open", namespaceFilter: "Other");

        Assert.True(result.Total > 0);
        Assert.All(result.Hits, hit => Assert.Equal("SampleSdk", hit.Namespace));
        Assert.Equal(0, missing.Total);
    }

    [Fact]
    public async Task PaginatesAndMarksTruncation()
    {
        var first = await GetAsync("Open", limit: 1);
        var second = await GetAsync("Open", offset: 1, limit: 1);

        Assert.Single(first.Hits);
        Assert.Single(second.Hits);
        Assert.True(first.Truncated);
        Assert.NotEqual(first.Hits[0].QualifiedName, second.Hits[0].QualifiedName);
    }

    [Fact]
    public async Task RejectsShortQueries()
    {
        var exception = await Assert.ThrowsAsync<SearchException>(() => GetAsync("O"));

        Assert.Equal(SearchErrorCodes.QueryTooShort, exception.Code);
    }

    [Fact]
    public async Task UnknownQueryReturnsEmptyResult()
    {
        var result = await GetAsync("Zzz");

        Assert.Empty(result.Hits);
        Assert.Equal(0, result.Total);
        Assert.False(result.Truncated);
    }

    private async Task<SearchResult> GetAsync(
        string query,
        string? kind = null,
        string? namespaceFilter = null,
        int offset = 0,
        int limit = QueryLimits.DefaultSearchLimit)
    {
        var snapshot = Assert.IsType<WorkspaceSnapshot>(_snapshot);
        return await SearchQuery.GetAsync(
            snapshot,
            query,
            kind,
            namespaceFilter,
            offset,
            limit,
            CancellationToken.None);
    }
}
