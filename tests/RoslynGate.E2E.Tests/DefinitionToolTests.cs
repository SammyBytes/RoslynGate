using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

[Collection(McpTestGroup.Name)]
public sealed class DefinitionToolTests(McpFixture fixture)
{
    [Fact]
    public async Task ResolvesDefinitionByNameInCompactMode()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetSymbolDefinition,
            new { name = "SampleSdk.Widget.Echo" });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
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
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
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
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
        var symbol = text.GetProperty("symbol");
        Assert.Equal("SampleSdk", symbol.GetProperty("namespace").GetString());
        Assert.True(symbol.GetProperty("location").GetProperty("column").GetInt32() > 0);
    }

    [Fact]
    public async Task MissingDefinitionSuggestsSearch()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetSymbolDefinition,
            new { name = "SampleSdk.Widget.Nope" });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
        var suggestion = text.GetProperty("try");
        Assert.Equal(ToolNames.SearchSymbols, suggestion.GetProperty("tool").GetString());
        Assert.Equal("Nope", suggestion.GetProperty("arguments").GetProperty("query").GetString());
    }

    [Fact]
    public async Task InvalidTargetReturnsActionableError()
    {
        using var response = await fixture.Session.CallToolAsync(ToolNames.GetSymbolDefinition, new { });
        var text = ToolTestHelpers.ToolText(response);

        Assert.True(ToolTestHelpers.IsError(response));
        var error = text.GetProperty("error");
        Assert.Equal(DefinitionErrorCodes.InvalidTarget, error.GetProperty("code").GetString());
        Assert.True(error.TryGetProperty("hint", out _));
    }
}
