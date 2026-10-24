using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

[McpServerToolType]
public sealed class ReloadTool(Workspace workspace)
{
    [McpServerTool(
        Name = ToolNames.ReloadWorkspace,
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Reload the .NET solution after project or structural file changes.")]
    public async Task<CallToolResult> ReloadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await workspace.ReloadAsync(cancellationToken).ConfigureAwait(false);
            return ToolJson.Text(ReloadJson(snapshot.Solution.Projects.Count()));
        }
        catch (WorkspaceLoadException exception)
        {
            return ToolErrors.From(exception);
        }
    }

    private string ReloadJson(int projects)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("status", "reloaded");
            writer.WriteNumber("projects", projects);
            writer.WriteString("sdk", Workspace.Sdk?.Version ?? "MSBuild default");
            writer.WriteString("watch", workspace.WatchEnabled ? "on" : "off");
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
