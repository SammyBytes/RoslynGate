using System.Diagnostics;
using System.Text.Json;

namespace RoslynGate.E2E.Tests;

public sealed class McpSession : IAsyncDisposable
{
    private readonly Process _process;
    private int _nextId;

    private McpSession(Process process)
    {
        _process = process;
        _ = process.StandardError.ReadToEndAsync();
    }

    public static async Task<McpSession> StartAsync(string workingDirectory, params string[] args)
    {
        var session = new McpSession(CliRunner.Start(workingDirectory, args));
        await session.SendRequestAsync(
            "initialize",
            new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "test", version = "0" }
            });
        await session.SendNotificationAsync("notifications/initialized");
        return session;
    }

    public async Task<JsonDocument> SendRequestAsync(string method, object? parameters = null)
    {
        var id = ++_nextId;
        var request = new Dictionary<string, object?>
        {
            ["jsonrpc"] = TestProtocol.JsonRpcVersion,
            ["id"] = id,
            ["method"] = method
        };

        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        await WriteAsync(request);
        var response = await ReadAsync();
        Assert.Equal(id, response.RootElement.GetProperty("id").GetInt32());
        return response;
    }

    public Task<JsonDocument> CallToolAsync(string tool, object arguments)
        => SendRequestAsync("tools/call", new { name = tool, arguments });

    public Task SendNotificationAsync(string method)
        => WriteAsync(new Dictionary<string, object?>
        {
            ["jsonrpc"] = TestProtocol.JsonRpcVersion,
            ["method"] = method
        });

    private async Task WriteAsync(object value)
        => await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value));

    private async Task<JsonDocument> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(TestProtocol.MessageTimeout);
        var line = await _process.StandardOutput.ReadLineAsync(timeout.Token);

        Assert.False(string.IsNullOrWhiteSpace(line), "Expected a JSON-RPC line on stdout.");

        var document = JsonDocument.Parse(line);
        Assert.Equal(
            TestProtocol.JsonRpcVersion,
            document.RootElement.GetProperty("jsonrpc").GetString());
        return document;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _process.StandardInput.Close();
            await _process.WaitForExitAsync();
        }
        finally
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                await _process.WaitForExitAsync();
            }

            _process.Dispose();
        }
    }
}
