using RoslynGate.Mcp;

namespace RoslynGate.Mcp.Tests;

public sealed class ContractTests
{
    [Fact]
    public void BuildInfoUsesTheProjectVersion()
    {
        var assemblyVersion = typeof(BuildInfo).Assembly.GetName().Version;
        Assert.Equal(assemblyVersion?.ToString(3), BuildInfo.Version);
    }

    [Fact]
    public void InstructionsNameEveryTool()
    {
        var instructions = Instructions.Text;

        Assert.Contains("get_symbol_definition", instructions);
        Assert.Contains("find_symbol_references", instructions);
        Assert.Contains("get_type_hierarchy", instructions);
        Assert.Contains("search_symbols", instructions);
        Assert.Contains("reload_workspace", instructions);
    }
}
