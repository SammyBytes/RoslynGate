using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

[Collection(McpTestGroup.Name)]
public sealed class ReferenceToolTests(McpFixture fixture)
{
    [Fact]
    public async Task FindsReferencesByPosition()
    {
        var line = FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)");
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.FindSymbolReferences,
            new { file = "SampleSdk/Widget.cs", line, limit = 10 });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
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
        var text = ToolTestHelpers.ToolText(response);

        Assert.Equal(1, text.GetProperty("refs").GetArrayLength());
        Assert.True(text.GetProperty("truncated").GetBoolean());
        Assert.Equal(1, text.GetProperty("nextOffset").GetInt32());
        Assert.Contains(
            "offset=1",
            text.GetProperty("hint").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteReferencePageOmitsNextOffset()
    {
        var line = FixtureSource.LineOf("SampleSdk/Widget.cs", "public Task Open(string doorId)");
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.FindSymbolReferences,
            new { file = "SampleSdk/Widget.cs", line, limit = 200 });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(text.GetProperty("truncated").GetBoolean());
        Assert.False(text.TryGetProperty("nextOffset", out _));
        Assert.False(text.TryGetProperty("hint", out _));
    }

    [Fact]
    public async Task ReferencesReturnCandidatesForOverloads()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.FindSymbolReferences,
            new { name = "SampleSdk.Widget.Open" });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
        Assert.True(text.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(2, text.GetProperty("candidates").GetArrayLength());
    }
}
