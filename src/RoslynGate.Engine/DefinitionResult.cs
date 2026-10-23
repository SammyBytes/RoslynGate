namespace RoslynGate.Engine;

public sealed record SymbolLocation(string File, int Line, int Column);

public sealed record SymbolDefinition(
    string Name,
    string QualifiedName,
    string Kind,
    string? Namespace,
    string? ContainingType,
    string Signature,
    string? Documentation,
    SymbolLocation Location);

public sealed record DefinitionResult(
    SymbolDefinition? Symbol,
    bool Ambiguous,
    IReadOnlyList<SymbolDefinition> Candidates);
