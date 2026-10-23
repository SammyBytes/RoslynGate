namespace RoslynGate.E2E.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class McpTestGroup : ICollectionFixture<McpFixture>
{
    public const string Name = "Mcp";
}
