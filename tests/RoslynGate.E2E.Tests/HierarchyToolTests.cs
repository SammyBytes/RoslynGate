using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

[Collection(McpTestGroup.Name)]
public sealed class HierarchyToolTests(McpFixture fixture)
{
    [Fact]
    public async Task ReturnsTypeHierarchy()
    {
        using var response = await fixture.Session.CallToolAsync(
            ToolNames.GetTypeHierarchy,
            new { name = "SampleSdk.Widget" });
        var text = ToolTestHelpers.ToolText(response);

        Assert.False(ToolTestHelpers.IsError(response));
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
        var text = ToolTestHelpers.ToolText(response);
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
        var text = ToolTestHelpers.ToolText(response);

        Assert.True(ToolTestHelpers.IsError(response));
        Assert.Equal(
            DefinitionErrorCodes.NotAType,
            text.GetProperty("error").GetProperty("code").GetString());
    }
}
