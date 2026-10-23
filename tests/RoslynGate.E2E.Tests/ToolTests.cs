using System.Text.Json;
using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

public sealed class ToolTests(McpFixture fixture) : IClassFixture<McpFixture>
{
    [Fact]
    public async Task ListsRegisteredTools()
    {
        using var response = await fixture.Session.SendRequestAsync("tools/list");
        var tools = response.RootElement.GetProperty("result").GetProperty("tools");

        Assert.Equal(3, tools.GetArrayLength());
        var names = Enumerable.Range(0, tools.GetArrayLength())
            .Select(index => tools[index].GetProperty("name").GetString())
            .ToArray();
        Assert.Contains(ToolNames.GetSymbolDefinition, names);
        Assert.Contains(ToolNames.FindSymbolReferences, names);
        Assert.Contains(ToolNames.GetTypeHierarchy, names);

        var definition = tools.EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == ToolNames.GetSymbolDefinition);
        var properties = definition.GetProperty("inputSchema").GetProperty("properties");
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
    public async Task FindsReferencesByPosition()
    {
        var line = FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)");
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.FindSymbolReferences,
            new { file = "SampleSdk/Widget.cs", line, limit = 10 });
        var text = ToolText(response);

        Assert.False(IsError(response));
        Assert.Equal("SampleSdk.Widget.Open", text.GetProperty("q").GetString());
        Assert.True(text.GetProperty("total").GetInt32() >= 2);
        Assert.True(text.GetProperty("refs").GetArrayLength() > 0);

        var reference = text.GetProperty("refs")[0];
        Assert.Equal("SampleSdk/DoorController.cs", reference[0].GetString());
        Assert.True(reference[1].GetInt32() > 0);
        Assert.Equal("call", reference[2].GetString());
    }

    [Fact]
    public async Task ReferencesRespectThePageLimit()
    {
        var line = FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)");
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.FindSymbolReferences,
            new { file = "SampleSdk/Widget.cs", line, limit = 1 });
        var text = ToolText(response);

        Assert.Equal(1, text.GetProperty("refs").GetArrayLength());
        Assert.True(text.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task ReferencesReturnCandidatesForOverloads()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.FindSymbolReferences,
            new { name = "SampleSdk.Widget.Open" });
        var text = ToolText(response);

        Assert.False(IsError(response));
        Assert.True(text.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(2, text.GetProperty("candidates").GetArrayLength());
    }

    [Fact]
    public async Task ReturnsTypeHierarchy()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetTypeHierarchy,
            new { name = "SampleSdk.Widget" });
        var text = ToolText(response);

        Assert.False(IsError(response));
        Assert.Equal("SampleSdk.Widget", text.GetProperty("t").GetString());
        Assert.Contains(
            "SampleSdk.DeviceBase",
            text.GetProperty("b").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains(
            "SampleSdk.IOpenable",
            text.GetProperty("i").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains(
            "SampleSdk.SecureWidget",
            text.GetProperty("d").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task ReturnsInterfaceImplementations()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetTypeHierarchy,
            new { name = "SampleSdk.IOpenable" });
        var text = ToolText(response);
        var implementations = text.GetProperty("m")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Contains("SampleSdk.Widget", implementations);
        Assert.Contains("SampleSdk.Gate", implementations);
    }

    [Fact]
    public async Task HierarchyRejectsNonTypeSymbols()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetTypeHierarchy,
            new { name = "SampleSdk.Widget.Echo" });
        var text = ToolText(response);

        Assert.True(IsError(response));
        Assert.Equal(
            DefinitionErrorCodes.NotAType,
            text.GetProperty("error").GetProperty("code").GetString());
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
