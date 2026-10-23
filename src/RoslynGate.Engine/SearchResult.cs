namespace RoslynGate.Engine;

public sealed record SearchResult(
    IReadOnlyList<SymbolDefinition> Hits,
    int Total,
    int Offset,
    int Limit,
    bool Truncated);
