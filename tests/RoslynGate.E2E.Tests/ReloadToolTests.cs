using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

[Collection(McpTestGroup.Name)]
public sealed class ReloadToolTests(McpFixture fixture)
{
    [Fact]
    public async Task ReloadsTheWorkspace()
    {
        using var response = await fixture.Session.CallToolAsync(ToolNames.ReloadWorkspace, new { });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
        Assert.Equal("reloaded", text.GetProperty("status").GetString());
        Assert.Equal(TestPaths.SampleProjectCount, text.GetProperty("projects").GetInt32());
    }
}
