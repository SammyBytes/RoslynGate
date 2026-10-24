using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

[Collection(McpTestGroup.Name)]
public sealed class ToolCatalogTests(McpFixture fixture)
{
    [Fact]
    public async Task ListsRegisteredTools()
    {
        using var response = await fixture.Session.SendRequestAsync("tools/list");
        var tools = response.RootElement.GetProperty("result").GetProperty("tools");

        Assert.Equal(5, tools.GetArrayLength());
        var names = Enumerable.Range(0, tools.GetArrayLength())
            .Select(index => tools[index].GetProperty("name").GetString())
            .ToArray();
        Assert.Contains(ToolNames.GetSymbolDefinition, names);
        Assert.Contains(ToolNames.FindSymbolReferences, names);
        Assert.Contains(ToolNames.GetTypeHierarchy, names);
        Assert.Contains(ToolNames.SearchSymbols, names);
        Assert.Contains(ToolNames.ReloadWorkspace, names);

        var definition = tools.EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == ToolNames.GetSymbolDefinition);
        var properties = definition.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("file", out _));
        Assert.True(properties.TryGetProperty("line", out _));
        Assert.True(properties.TryGetProperty("detail", out _));
    }
}
