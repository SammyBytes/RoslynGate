using System.Text.Json;
using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

public sealed class ToolTests(McpFixture fixture) : IClassFixture<McpFixture>
{
    [Fact]
    public async Task ListsDefinitionTool()
    {
        using var response = await fixture.Session.SendRequestAsync("tools/list");
        var tools = response.RootElement.GetProperty("result").GetProperty("tools");

        Assert.Equal(1, tools.GetArrayLength());
        var tool = tools[0];
        Assert.Equal(ToolNames.GetSymbolDefinition, tool.GetProperty("name").GetString());

        var properties = tool.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("file", out _));
        Assert.True(properties.TryGetProperty("line", out _));
        Assert.True(properties.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task ResolvesDefinitionByNameInCompactMode()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetSymbolDefinition,
            new { name = "SampleSdk.Widget.Echo" });
        var text = ToolText(response);

        Assert.False(IsError(response));
        Assert.Equal("Echo", text.GetProperty("n").GetString());
        Assert.Equal("SampleSdk.Widget.Echo", text.GetProperty("q").GetString());
        Assert.Equal("method", text.GetProperty("k").GetString());
        Assert.EndsWith(".cs", text.GetProperty("f").GetString(), StringComparison.Ordinal);
        Assert.True(text.GetProperty("l").GetInt32() > 0);
    }

    [Fact]
    public async Task ReturnsCandidatesForOverloads()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetSymbolDefinition,
            new { name = "SampleSdk.Widget.Open" });
        var text = ToolText(response);

        Assert.False(IsError(response));
        Assert.True(text.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(2, text.GetProperty("candidates").GetArrayLength());
        Assert.True(text.TryGetProperty("hint", out _));
    }

    [Fact]
    public async Task VerboseDetailAddsNamespaceAndColumn()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetSymbolDefinition,
            new { name = "SampleSdk.Widget.Echo", detail = "verbose" });
        var text = ToolText(response);

        Assert.False(IsError(response));
        var symbol = text.GetProperty("symbol");
        Assert.Equal("SampleSdk", symbol.GetProperty("namespace").GetString());
        Assert.True(symbol.GetProperty("location").GetProperty("column").GetInt32() > 0);
    }

    [Fact]
    public async Task InvalidTargetReturnsActionableError()
    {
        using var response = await fixture.Session.CallToolAsync(ToolNames.GetSymbolDefinition, new { });
        var text = ToolText(response);

        Assert.True(IsError(response));
        var error = text.GetProperty("error");
        Assert.Equal(DefinitionErrorCodes.InvalidTarget, error.GetProperty("code").GetString());
        Assert.True(error.TryGetProperty("hint", out _));
    }

    private static bool IsError(JsonDocument response)
        => response.RootElement
            .GetProperty("result")
            .TryGetProperty("isError", out var isError)
            && isError.GetBoolean();

    private static JsonElement ToolText(JsonDocument response)
    {
        var content = response.RootElement.GetProperty("result").GetProperty("content");
        var text = content[0].GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }
}
