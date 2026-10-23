using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

[Collection(WorkspaceTestGroup.Name)]
public sealed class HierarchyQueryTests : IAsyncLifetime, IDisposable
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
    public async Task ReturnsBaseInterfacesAndDerivedTypes()
    {
        var result = await GetAsync("SampleSdk.Widget");

        var type = Assert.IsType<TypeHierarchy>(result.Type);
        Assert.Contains("SampleSdk.DeviceBase", type.BaseTypes);
        Assert.Contains("SampleSdk.IOpenable", type.Interfaces);
        Assert.Contains("SampleSdk.SecureWidget", type.DerivedTypes);
        Assert.Empty(type.Implementations);
    }

    [Fact]
    public async Task ReturnsImplementationsForAnInterface()
    {
        var result = await GetAsync("SampleSdk.IOpenable");

        var type = Assert.IsType<TypeHierarchy>(result.Type);
        Assert.Contains("SampleSdk.Widget", type.Implementations);
        Assert.Contains("SampleSdk.Gate", type.Implementations);
    }

    [Fact]
    public async Task RejectsNonTypeSymbols()
    {
        var exception = await Assert.ThrowsAsync<DefinitionException>(
            () => GetAsync("SampleSdk.Widget.Echo"));

        Assert.Equal(DefinitionErrorCodes.NotAType, exception.Code);
    }

    [Fact]
    public async Task UnknownTypeReturnsEmptyHierarchy()
    {
        var result = await GetAsync("SampleSdk.Missing");

        Assert.Null(result.Type);
        Assert.False(result.Ambiguous);
    }

    private async Task<HierarchyResult> GetAsync(string name)
    {
        var snapshot = Assert.IsType<WorkspaceSnapshot>(_snapshot);
        return await HierarchyQuery.GetAsync(
            snapshot,
            new SymbolTarget(name, null, null, null),
            CancellationToken.None);
    }
}
