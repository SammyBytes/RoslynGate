using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class ToolErrors
{
    public static CallToolResult From(QueryException exception)
        => ToolJson.Error(new ToolError(exception.Code, exception.Message, exception.Hint));

    public static CallToolResult From(SolutionNotFoundException exception)
        => ToolJson.Error(new ToolError(
            ToolErrorCodes.SolutionNotFound,
            exception.Message,
            "Pass --solution when starting the server, or run roslyngate --check."));

    public static CallToolResult From(WorkspaceLoadException exception)
        => ToolJson.Error(new ToolError(
            ToolErrorCodes.SolutionLoadFailed,
            exception.Message,
            exception.Hint ?? "Run roslyngate --check to inspect the solution."));
}
