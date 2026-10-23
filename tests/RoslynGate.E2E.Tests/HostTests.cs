using System.Diagnostics;
using System.Text.Json;

namespace RoslynGate.E2E.Tests;

public sealed class HostTests
{
    [Fact]
    public async Task InitializeToolsListAndPingProduceOnlyJsonRpcOnStdout()
    {
        using var process = CliRunner.Start(AppContext.BaseDirectory);
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            await process.StandardInput.WriteLineAsync(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"0"}}}""");
            using var initialize = await ReadMessageAsync(process);

            Assert.Equal(1, initialize.RootElement.GetProperty("id").GetInt32());
            var serverInfo = initialize.RootElement.GetProperty("result").GetProperty("serverInfo");
            Assert.Equal("roslyngate", serverInfo.GetProperty("name").GetString());

            await process.StandardInput.WriteLineAsync(
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await process.StandardInput.WriteLineAsync(
                """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
            using var toolsList = await ReadMessageAsync(process);

            Assert.Equal(2, toolsList.RootElement.GetProperty("id").GetInt32());
            var tools = toolsList.RootElement.GetProperty("result").GetProperty("tools");
            Assert.Equal(0, tools.GetArrayLength());

            await process.StandardInput.WriteLineAsync(
                """{"jsonrpc":"2.0","id":3,"method":"ping"}""");
            using var ping = await ReadMessageAsync(process);

            Assert.Equal(3, ping.RootElement.GetProperty("id").GetInt32());
            Assert.Equal(JsonValueKind.Object, ping.RootElement.GetProperty("result").ValueKind);

            process.StandardInput.Close();
            await process.WaitForExitAsync();

            Assert.Equal(0, process.ExitCode);
            Assert.Empty(await stderr);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync();
            }
        }
    }

    [Fact]
    public async Task HelpWritesUsageToStdoutAndExitsZero()
    {
        var result = await CliRunner.RunAsync(AppContext.BaseDirectory, string.Empty, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task VersionWritesVersionToStdoutAndExitsZero()
    {
        var result = await CliRunner.RunAsync(AppContext.BaseDirectory, string.Empty, "--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("roslyngate 0.1.0", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task CheckSucceedsWhenExactlyOneSolutionExists()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Sample.sln"), string.Empty);

        var result = await CliRunner.RunAsync(directory.Root, string.Empty, "--check");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Sample.sln", result.Stdout);
        Assert.Contains("status      ok", result.Stdout);
        Assert.Empty(result.Stderr);
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

    private static async Task<JsonDocument> ReadMessageAsync(Process process)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var line = await process.StandardOutput.ReadLineAsync(timeout.Token);

        Assert.False(string.IsNullOrWhiteSpace(line), "Expected a JSON-RPC line on stdout.");

        var document = JsonDocument.Parse(line);
        Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        return document;
    }
}

internal sealed class TempDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        "roslyngate-tests",
        Guid.NewGuid().ToString("N"));

    public TempDirectory()
    {
        Directory.CreateDirectory(Root);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
