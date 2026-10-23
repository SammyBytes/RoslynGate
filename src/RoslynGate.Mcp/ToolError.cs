namespace RoslynGate.Mcp;

public sealed record ToolSuggestion(
    string Tool,
    IReadOnlyDictionary<string, object?> Arguments);

public sealed record ToolError(
    string Code,
    string Message,
    string Hint,
    ToolSuggestion? Try = null);
