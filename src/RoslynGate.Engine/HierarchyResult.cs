namespace RoslynGate.Engine;

public sealed record TypeHierarchy(
    SymbolDefinition Type,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> DerivedTypes,
    IReadOnlyList<string> Implementations);

public sealed record HierarchyResult(
    TypeHierarchy? Type,
    bool Ambiguous,
    IReadOnlyList<SymbolDefinition> Candidates);
