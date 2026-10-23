using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

[McpServerToolType]
public sealed class HierarchyTool(Workspace workspace, ServerOptions options)
{
    [McpServerTool(
        Name = ToolNames.GetTypeHierarchy,
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Return base types, interfaces, derived types, and implementations for a .NET type.")]
    public async Task<CallToolResult> GetTypeHierarchyAsync(
        [Description("Qualified or partial type name, e.g. SampleSdk.Widget. Ignored when file is provided.")]
        string? name = null,
        [Description("Solution-relative or absolute file path. Requires line.")]
        string? file = null,
        [Description("1-based line number.")]
        int? line = null,
        [Description("1-based column. Optional; defaults to the first token on the line.")]
        int? column = null,
        [Description("compact (default) or verbose.")]
        DetailLevel? detail = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await workspace.GetAsync(cancellationToken).ConfigureAwait(false);
            var result = await HierarchyQuery
                .GetAsync(snapshot, new SymbolTarget(name, file, line, column), cancellationToken)
                .ConfigureAwait(false);

            return HierarchyJson.Hierarchy(result, detail ?? options.Detail);
        }
        catch (DefinitionException exception)
        {
            return ToolJson.Error(exception.Code, exception.Message, exception.Hint);
        }
        catch (SolutionNotFoundException exception)
        {
            return ToolJson.Error(
                ToolErrorCodes.SolutionNotFound,
                exception.Message,
                "Pass --solution when starting the server, or run roslyngate --check.");
        }
        catch (WorkspaceLoadException exception)
        {
            return ToolJson.Error(
                ToolErrorCodes.SolutionLoadFailed,
                exception.Message,
                "Run roslyngate --check to inspect the solution.");
        }
    }
}
