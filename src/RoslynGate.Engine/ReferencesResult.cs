namespace RoslynGate.Engine;

public sealed record SymbolReference(string Kind, SymbolLocation Location);

public sealed record ReferencesResult(
    SymbolDefinition? Symbol,
    bool Ambiguous,
    IReadOnlyList<SymbolDefinition> Candidates,
    IReadOnlyList<SymbolReference> References,
    int Total,
    int Offset,
    int Limit,
    bool Truncated);
