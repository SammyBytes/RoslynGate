namespace RoslynGate.Engine;

public sealed record SymbolTarget(
    string? Name,
    string? File,
    int? Line,
    int? Column = null,
    IReadOnlyList<string>? ParameterTypes = null);
