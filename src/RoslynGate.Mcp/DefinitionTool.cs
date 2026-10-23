using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

[McpServerToolType]
public sealed class DefinitionTool(Workspace workspace, ServerOptions options)
{
    [McpServerTool(
        Name = ToolNames.GetSymbolDefinition,
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Resolve one .NET symbol by qualified name or by solution-relative file and 1-based line.")]
    public async Task<CallToolResult> GetSymbolDefinitionAsync(
        [Description("Qualified or partial symbol name, e.g. SampleSdk.Widget.Open. Ignored when file is provided.")]
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
            var result = await DefinitionQuery
                .GetAsync(snapshot, new SymbolTarget(name, file, line, column), cancellationToken)
                .ConfigureAwait(false);

            return DefinitionJson.Definition(result, detail ?? options.Detail);
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
