namespace RoslynGate.E2E.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public async Task EnvironmentVariablesProvideDefaults()
    {
        var environment = new Dictionary<string, string?>
        {
            ["ROSLYGATE_DETAIL"] = "verbose",
            ["ROSLYGATE_PREWARM"] = "off",
            ["ROSLYGATE_REF_LIMIT"] = "999"
        };

        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            environment,
            "--check");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("detail      verbose", result.Stdout);
        Assert.Contains("ref-limit   200", result.Stdout);
        Assert.Contains("prewarm     off", result.Stdout);
    }

    [Fact]
    public async Task CommandLineFlagsOverrideEnvironment()
    {
        var environment = new Dictionary<string, string?>
        {
            ["ROSLYGATE_DETAIL"] = "compact"
        };

        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            environment,
            "--check",
            "--detail",
            "verbose");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("detail      verbose", result.Stdout);
    }

    [Fact]
    public async Task InvalidEnvironmentFailsCheck()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Sample.sln"), string.Empty);
        var environment = new Dictionary<string, string?>
        {
            ["ROSLYGATE_DETAIL"] = "nope"
        };

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, environment, "--check");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid detail level", result.Stderr);
    }

    [Fact]
    public async Task InvalidEnvironmentDoesNotBreakHelp()
    {
        var environment = new Dictionary<string, string?>
        {
            ["ROSLYGATE_DETAIL"] = "nope"
        };

        var result = await CliRunner.RunAsync(
            AppContext.BaseDirectory,
            string.Empty,
            environment,
            "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Stdout);
    }

    [Fact]
    public async Task UnknownOptionFailsWithParseError()
    {
        var result = await CliRunner.RunAsync(AppContext.BaseDirectory, string.Empty, "--nope");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unrecognized", result.Stderr);
    }

    [Fact]
    public async Task ConflictingPrewarmFlagsFail()
    {
        var result = await CliRunner.RunAsync(
            AppContext.BaseDirectory,
            string.Empty,
            "--check",
            "--prewarm",
            "--no-prewarm");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("either --prewarm or --no-prewarm", result.Stderr);
    }
}
