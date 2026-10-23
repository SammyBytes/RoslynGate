using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

public sealed class WorkspaceTests
{
    [Fact]
    public async Task LoadsSampleSolutionAndCachesTheSnapshot()
    {
        using var workspace = new Workspace(TestPaths.SampleSolution);

        var first = await workspace.GetAsync(CancellationToken.None);
        var second = await workspace.GetAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(TestPaths.SampleSolution, first.SolutionPath);
        Assert.EndsWith("SampleSolution", first.RootPath, StringComparison.Ordinal);
        Assert.Equal(TestPaths.SampleProjectCount, first.Solution.Projects.Count());
    }

    [Fact]
    public async Task ConcurrentCallsShareOneLoad()
    {
        using var workspace = new Workspace(TestPaths.SampleSolution);

        var snapshots = await Task.WhenAll(
            Enumerable.Range(0, 6)
                .Select(_ => workspace.GetAsync(CancellationToken.None)));

        Assert.All(snapshots, snapshot => Assert.Same(snapshots[0], snapshot));
    }

    [Fact]
    public async Task CanceledWaitDoesNotPoisonTheWorkspace()
    {
        using var workspace = new Workspace(TestPaths.SampleSolution);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => workspace.GetAsync(cancellation.Token));

        var snapshot = await workspace.GetAsync(CancellationToken.None);

        Assert.Equal(TestPaths.SampleProjectCount, snapshot.Solution.Projects.Count());
    }

    [Fact]
    public async Task MissingSolutionFailsWithAnActionableError()
    {
        using var workspace = new Workspace(Path.Combine(Path.GetTempPath(), "missing-roslyngate.sln"));

        var exception = await Assert.ThrowsAsync<SolutionNotFoundException>(
            () => workspace.GetAsync(CancellationToken.None));

        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public async Task InvalidSolutionFailsWithALoadError()
    {
        var root = Path.Combine(Path.GetTempPath(), "roslyngate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var solution = Path.Combine(root, "Bad.sln");
        File.WriteAllText(solution, "not a solution");

        try
        {
            using var workspace = new Workspace(solution);

            var exception = await Assert.ThrowsAsync<WorkspaceLoadException>(
                () => workspace.GetAsync(CancellationToken.None));

            Assert.Contains("Failed to load", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
