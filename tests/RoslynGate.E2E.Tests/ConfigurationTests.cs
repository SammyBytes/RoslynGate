using RoslynGate.Cli;
using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public async Task EnvironmentVariablesProvideDefaults()
    {
        var environment = new Dictionary<string, string?>
        {
            [EnvironmentVariables.Detail] = "verbose",
            [EnvironmentVariables.Prewarm] = "off",
            [EnvironmentVariables.RefLimit] = "999"
        };

        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            environment,
            CliNames.Check);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Matches($@"{CheckText.Detail}\s+verbose", result.Stdout);
        Assert.Matches($@"{CheckText.RefLimit}\s+{ServerOptions.MaxReferenceLimit}", result.Stdout);
        Assert.Matches($@"{CheckText.Prewarm}\s+{CheckText.Off}", result.Stdout);
    }

    [Fact]
    public async Task CommandLineFlagsOverrideEnvironment()
    {
        var environment = new Dictionary<string, string?>
        {
            [EnvironmentVariables.Detail] = "compact"
        };

        var result = await CliRunner.RunAsync(
            TestPaths.SampleSolutionDirectory,
            string.Empty,
            environment,
            CliNames.Check,
            CliNames.Detail,
            "verbose");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Matches($@"{CheckText.Detail}\s+verbose", result.Stdout);
    }

    [Fact]
    public async Task InvalidEnvironmentFailsCheck()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Sample.sln"), string.Empty);
        var environment = new Dictionary<string, string?>
        {
            [EnvironmentVariables.Detail] = "nope"
        };

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, environment, CliNames.Check);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("Invalid detail level", result.Stderr);
    }

    [Fact]
    public async Task InvalidEnvironmentDoesNotBreakHelp()
    {
        var environment = new Dictionary<string, string?>
        {
            [EnvironmentVariables.Detail] = "nope"
        };

        var result = await CliRunner.RunAsync(
            AppContext.BaseDirectory,
            string.Empty,
            environment,
            "--help");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Usage:", result.Stdout);
    }

    [Fact]
    public async Task UnknownOptionFailsWithParseError()
    {
        var result = await CliRunner.RunAsync(AppContext.BaseDirectory, string.Empty, "--nope");

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains("Unrecognized", result.Stderr);
    }

    [Fact]
    public async Task ConflictingPrewarmFlagsFail()
    {
        var result = await CliRunner.RunAsync(
            AppContext.BaseDirectory,
            string.Empty,
            CliNames.Check,
            CliNames.Prewarm,
            CliNames.NoPrewarm);

        Assert.Equal(ExitCodes.InvalidConfiguration, result.ExitCode);
        Assert.Contains(CliNames.Prewarm, result.Stderr);
        Assert.Contains(CliNames.NoPrewarm, result.Stderr);
    }
}
