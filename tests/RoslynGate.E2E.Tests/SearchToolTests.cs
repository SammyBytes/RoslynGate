using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

[Collection(McpTestGroup.Name)]
public sealed class SearchToolTests(McpFixture fixture)
{
    [Fact]
    public async Task SearchesByNameFragment()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.SearchSymbols,
            new { query = "Widget" });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
        Assert.True(text.GetProperty("total").GetInt32() > 0);
        var hits = text.GetProperty("hits");
        Assert.True(hits.GetArrayLength() > 0);
        Assert.Equal("SampleSdk.Widget", hits[0][0].GetString());
        Assert.Equal("class", hits[0][1].GetString());
    }

    [Fact]
    public async Task SearchFiltersByKind()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.SearchSymbols,
            new { query = "Open", kind = "method", limit = 20 });
        var text = ToolTestHelpers.ToolText(response);
        var hits = text.GetProperty("hits");

        Assert.True(hits.GetArrayLength() > 0);
        Assert.All(
            hits.EnumerateArray(),
            hit => Assert.Equal("method", hit[1].GetString()));
    }

    [Fact]
    public async Task SearchRespectsTheLimit()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.SearchSymbols,
            new { query = "Open", limit = 1 });
        var text = ToolTestHelpers.ToolText(response);

        Assert.Single(text.GetProperty("hits").EnumerateArray());
        Assert.True(text.GetProperty("truncated").GetBoolean());
        Assert.True(text.TryGetProperty("hint", out _));
    }

    [Fact]
    public async Task SearchRejectsShortQueries()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.SearchSymbols,
            new { query = "O" });
        var text = ToolTestHelpers.ToolText(response);

        Assert.True(ToolTestHelpers.IsError(response));
        Assert.Equal(
            SearchErrorCodes.QueryTooShort,
            text.GetProperty("error").GetProperty("code").GetString());
    }
}
