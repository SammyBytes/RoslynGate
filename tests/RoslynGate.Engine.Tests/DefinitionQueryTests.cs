using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

[Collection(WorkspaceTestGroup.Name)]
public sealed class DefinitionQueryTests : IAsyncLifetime, IDisposable
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
    public async Task ResolvesTypeByQualifiedName()
    {
        var result = await GetAsync(name: "SampleSdk.Widget");

        var symbol = Assert.IsType<SymbolDefinition>(result.Symbol);
        Assert.False(result.Ambiguous);
        Assert.Equal("Widget", symbol.Name);
        Assert.Equal("SampleSdk.Widget", symbol.QualifiedName);
        Assert.Equal("class", symbol.Kind);
        Assert.StartsWith("SampleSdk/Widget", symbol.Location.File, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolvesMethodUnambiguously()
    {
        var result = await GetAsync(name: "SampleSdk.Widget.Echo");

        var symbol = Assert.IsType<SymbolDefinition>(result.Symbol);
        Assert.Equal("Echo", symbol.Name);
        Assert.Equal("method", symbol.Kind);
        Assert.Contains("Echo", symbol.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnsCandidatesForOverloads()
    {
        var result = await GetAsync(name: "SampleSdk.Widget.Open");

        Assert.True(result.Ambiguous);
        Assert.Null(result.Symbol);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(
            result.Candidates,
            candidate => Assert.Equal("SampleSdk.Widget.Open", candidate.QualifiedName));
        Assert.Equal(
            result.Candidates.OrderBy(candidate => candidate.Signature, StringComparer.Ordinal),
            result.Candidates);
    }

    [Fact]
    public async Task UnqualifiedNameReturnsCandidates()
    {
        var result = await GetAsync(name: "Open");

        Assert.True(result.Ambiguous);
        Assert.Contains(result.Candidates, candidate => candidate.QualifiedName == "SampleSdk.Widget.Open");
        Assert.Contains(result.Candidates, candidate => candidate.QualifiedName == "SampleSdk.Gate.Open");
    }

    [Fact]
    public async Task ResolvesDeclarationByPosition()
    {
        var line = LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)");

        var result = await GetAsync(file: "SampleSdk/Widget.cs", line: line);

        var symbol = Assert.IsType<SymbolDefinition>(result.Symbol);
        Assert.Equal("Open", symbol.Name);
        Assert.Contains("public", symbol.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolvesReferenceByPosition()
    {
        var line = LineOf("SampleSdk/DoorController.cs", "widget.Open(\"door-1\")");
        var column = ColumnOf("SampleSdk/DoorController.cs", "Open(\"door-1\")");

        var result = await GetAsync(
            file: "SampleSdk/DoorController.cs",
            line: line,
            column: column);

        var symbol = Assert.IsType<SymbolDefinition>(result.Symbol);
        Assert.Equal("Open", symbol.Name);
        Assert.Equal("SampleSdk.Widget.Open", symbol.QualifiedName);
    }

    [Fact]
    public async Task UnknownNameReturnsEmptyResult()
    {
        var result = await GetAsync(name: "SampleSdk.Widget.DoesNotExist");

        Assert.Null(result.Symbol);
        Assert.False(result.Ambiguous);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task MissingFileThrowsActionableError()
    {
        var exception = await Assert.ThrowsAsync<DefinitionException>(
            () => GetAsync(file: "SampleSdk/Missing.cs", line: 1));

        Assert.Equal(DefinitionErrorCodes.FileNotFound, exception.Code);
        Assert.Contains("relative to the solution root", exception.Hint);
    }

    [Fact]
    public async Task LineOutOfRangeThrowsActionableError()
    {
        var exception = await Assert.ThrowsAsync<DefinitionException>(
            () => GetAsync(file: "SampleSdk/Widget.cs", line: 9999));

        Assert.Equal(DefinitionErrorCodes.LineOutOfRange, exception.Code);
        Assert.Contains("1 and", exception.Hint);
    }

    [Fact]
    public async Task FileWithoutLineThrowsActionableError()
    {
        var exception = await Assert.ThrowsAsync<DefinitionException>(
            () => GetAsync(file: "SampleSdk/Widget.cs"));

        Assert.Equal(DefinitionErrorCodes.PositionIncomplete, exception.Code);
    }

    [Fact]
    public async Task MissingTargetThrowsActionableError()
    {
        var exception = await Assert.ThrowsAsync<DefinitionException>(
            () => GetAsync());

        Assert.Equal(DefinitionErrorCodes.InvalidTarget, exception.Code);
    }

    private async Task<DefinitionResult> GetAsync(
        string? name = null,
        string? file = null,
        int? line = null,
        int? column = null)
    {
        var snapshot = Assert.IsType<WorkspaceSnapshot>(_snapshot);
        return await DefinitionQuery.GetAsync(
            snapshot,
            new SymbolTarget(name, file, line, column),
            CancellationToken.None);
    }

    private static int LineOf(string relativePath, string text)
    {
        var lines = File.ReadAllLines(Path.Combine(TestPaths.SampleSolutionDirectory, relativePath));
        var index = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        return index < 0
            ? throw new InvalidOperationException($"Could not find '{text}' in {relativePath}.")
            : index + 1;
    }

    private static int ColumnOf(string relativePath, string text)
    {
        var lines = File.ReadAllLines(Path.Combine(TestPaths.SampleSolutionDirectory, relativePath));
        var line = lines.First(line => line.Contains(text, StringComparison.Ordinal));
        return line.IndexOf(text, StringComparison.Ordinal) + 1;
    }
}
