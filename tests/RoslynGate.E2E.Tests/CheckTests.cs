namespace RoslynGate.E2E.Tests;

public sealed class CheckTests
{
    [Fact]
    public async Task CheckLoadsSampleSolution()
    {
        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            "--check");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SampleSolution.sln", result.Stdout);
        Assert.Contains("projects    2", result.Stdout);
        Assert.Contains("status      ok", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task CheckReportsLoadFailure()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Bad.sln"), "not a solution");

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, "--check");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Failed to load", result.Stderr);
    }

    [Fact]
    public async Task InvalidSolutionExitsOneBeforeStartingTheHost()
    {
        var result = await CliRunner.RunAsync(
            AppContext.BaseDirectory,
            string.Empty,
            "--solution",
            Path.Combine(Path.GetTempPath(), "missing-roslyngate.sln"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("does not exist", result.Stderr);
    }

    [Fact]
    public async Task CheckFailsWithActionableMessageWhenNoSolutionExists()
    {
        using var directory = new TempDirectory();

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, "--check");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No .sln or .slnx", result.Stderr);
    }

    [Fact]
    public async Task SolutionEqualsFormAndNoPrewarmAreAccepted()
    {
        var result = await CliRunner.RunAsync(
            TestPaths.RepositoryRoot,
            string.Empty,
            "--check",
            $"--solution={TestPaths.SampleSolution}",
            "--no-prewarm");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("prewarm     off", result.Stdout);
    }
}
