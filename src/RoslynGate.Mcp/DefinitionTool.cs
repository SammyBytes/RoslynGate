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

            return DefinitionJson.Definition(
                result,
                detail ?? options.Detail,
                ToolJson.SearchQueryFor(name));
        }
        catch (QueryException exception)
        {
            return ToolErrors.From(exception);
        }
        catch (SolutionNotFoundException exception)
        {
            return ToolErrors.From(exception);
        }
        catch (WorkspaceLoadException exception)
        {
            return ToolErrors.From(exception);
        }
    }
}
