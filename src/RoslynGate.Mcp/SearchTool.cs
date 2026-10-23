using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

[McpServerToolType]
public sealed class SearchTool(Workspace workspace, ServerOptions options)
{
    [McpServerTool(
        Name = ToolNames.SearchSymbols,
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Search types and members by name fragment before resolving a symbol.")]
    public async Task<CallToolResult> SearchSymbolsAsync(
        [Description("Name fragment with at least 2 characters, e.g. Widget or Open.")]
        string? query = null,
        [Description("Optional kind filter: class, interface, method, property, field, enum, struct, delegate.")]
        string? kind = null,
        [Description("Optional namespace prefix filter, e.g. SampleSdk.")]
        string? @namespace = null,
        [Description("Hits to skip. Defaults to 0.")]
        int? offset = null,
        [Description("Hits to return. Defaults to the server search limit.")]
        int? limit = null,
        [Description("compact (default) or verbose.")]
        DetailLevel? detail = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await workspace.GetAsync(cancellationToken).ConfigureAwait(false);
            var result = await SearchQuery
                .GetAsync(
                    snapshot,
                    query,
                    kind,
                    @namespace,
                    offset ?? 0,
                    limit ?? options.SearchLimit,
                    cancellationToken)
                .ConfigureAwait(false);

            return SearchJson.Search(result, detail ?? options.Detail);
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
