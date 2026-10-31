using RoslynGate.Cli;

namespace RoslynGate.E2E.Tests;

public sealed class CheckTests
{
    [Fact]
    public async Task CheckLoadsSampleSolution()
    {
        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            CliNames.Check);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains(TestPaths.SampleSolutionFileName, result.Stdout);
        Assert.Matches($@"{CheckText.Sdk}\s+\S+", result.Stdout);
        Assert.Matches($@"{CheckText.SdkPath}\s+.+", result.Stdout);
        Assert.Matches($@"{CheckText.Projects}\s+{TestPaths.SampleProjectCount}", result.Stdout);
        Assert.Matches($@"{CheckText.Status}\s+{CheckText.Ok}", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task CheckReportsLoadFailure()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Bad.sln"), "not a solution");

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, CliNames.Check);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("Failed to load", result.Stderr);
    }

    [Fact]
    public async Task InvalidSolutionExitsOneBeforeStartingTheHost()
    {
        var result = await CliRunner.RunAsync(
            AppContext.BaseDirectory,
            string.Empty,
            CliNames.Solution,
            Path.Combine(Path.GetTempPath(), "missing-roslyngate.sln"));

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("does not exist", result.Stderr);
    }

    [Fact]
    public async Task CheckFailsWithActionableMessageWhenNoSolutionExists()
    {
        using var directory = new TempDirectory();

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, CliNames.Check);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("No .sln or .slnx", result.Stderr);
    }

    [Fact]
    public async Task SolutionEqualsFormAndNoPrewarmAreAccepted()
    {
        var result = await CliRunner.RunAsync(
            TestPaths.RepositoryRoot,
            string.Empty,
            CliNames.Check,
            $"{CliNames.Solution}={TestPaths.SampleSolution}",
            CliNames.NoPrewarm);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Matches($@"{CheckText.Prewarm}\s+{CheckText.Off}", result.Stdout);
    }

    [Fact]
    public async Task PointsToTypeScriptPackageWhenOnlyTsconfigExists()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "tsconfig.json"), "{}");

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, CliNames.Check);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("TypeScript workspace", result.Stderr);
        Assert.Contains("roslyngate-ts", result.Stderr);
    }

    [Fact]
    public async Task TypeScriptFlagPointsToTheOtherPackage()
    {
        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            CliNames.TypeScript);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("roslyngate-ts", result.Stderr);
    }

    [Fact]
    public async Task DotNetFlagKeepsTheSolutionError()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "tsconfig.json"), "{}");

        var result = await CliRunner.RunAsync(
            directory.Root,
            string.Empty,
            CliNames.DotNet,
            CliNames.Check);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("No .sln or .slnx", result.Stderr);
    }
}
