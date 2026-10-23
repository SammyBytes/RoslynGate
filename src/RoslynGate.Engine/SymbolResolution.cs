using Microsoft.CodeAnalysis;

namespace RoslynGate.Engine;

public sealed record ResolvedSymbol(ISymbol Symbol, SymbolDefinition Definition);

public sealed record SymbolResolution(IReadOnlyList<ResolvedSymbol> Matches)
{
    public bool Ambiguous => Matches.Count > 1;

    public ResolvedSymbol? OnlyMatch => Matches.Count == 1 ? Matches[0] : null;
}
