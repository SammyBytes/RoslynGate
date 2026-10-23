using RoslynGate.Mcp;

namespace RoslynGate.Mcp.Tests;

public sealed class ContractTests
{
    [Fact]
    public void BuildInfoUsesTheProjectVersion()
    {
        Assert.Equal("0.1.0", BuildInfo.Version);
    }

    [Fact]
    public void InstructionsNameEveryTool()
    {
        var instructions = Instructions.Text;

        Assert.Contains("get_symbol_definition", instructions);
        Assert.Contains("find_symbol_references", instructions);
        Assert.Contains("get_type_hierarchy", instructions);
        Assert.Contains("search_symbols", instructions);
    }
}
