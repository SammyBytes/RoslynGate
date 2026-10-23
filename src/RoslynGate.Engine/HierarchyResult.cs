namespace RoslynGate.Engine;

public sealed record TypeHierarchy(
    SymbolDefinition Type,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> DerivedTypes,
    IReadOnlyList<string> Implementations,
    bool Truncated);

public sealed record HierarchyResult(
    TypeHierarchy? Type,
    bool Ambiguous,
    IReadOnlyList<SymbolDefinition> Candidates);
