using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace RoslynGate.Engine;

public static class HierarchyQuery
{
    public static async Task<HierarchyResult> GetAsync(
        WorkspaceSnapshot snapshot,
        SymbolTarget target,
        CancellationToken cancellationToken)
    {
        var resolution = await SymbolResolver
            .ResolveAsync(snapshot, target, cancellationToken)
            .ConfigureAwait(false);

        if (resolution.Matches.Count == 0)
        {
            return new HierarchyResult(null, Ambiguous: false, Array.Empty<SymbolDefinition>());
        }

        if (resolution.Matches.Count > 1)
        {
            return new HierarchyResult(
                null,
                Ambiguous: true,
                resolution.Matches
                    .Take(DefinitionLimits.MaxCandidates)
                    .Select(match => match.Definition)
                    .ToArray());
        }

        if (resolution.Matches[0].Symbol is not INamedTypeSymbol type)
        {
            throw new DefinitionException(
                DefinitionErrorCodes.NotAType,
                "The symbol is not a type.",
                "Use a class, interface, struct, enum, or delegate.");
        }

        var derivedTypes = await SymbolFinder
            .FindDerivedClassesAsync(type, snapshot.Solution, projects: null, cancellationToken)
            .ConfigureAwait(false);

        var implementations = type.TypeKind == TypeKind.Interface
            ? await SymbolFinder
                .FindImplementationsAsync(type, snapshot.Solution, projects: null, cancellationToken)
                .ConfigureAwait(false)
            : [];

        return new HierarchyResult(
            new TypeHierarchy(
                resolution.Matches[0].Definition,
                BaseTypesOf(type),
                type.AllInterfaces
                    .Select(SymbolFormatting.BuildQualifiedName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray(),
                OrderedNames(derivedTypes),
                OrderedNames(implementations)),
            Ambiguous: false,
            Array.Empty<SymbolDefinition>());
    }

    private static List<string> BaseTypesOf(INamedTypeSymbol type)
    {
        var names = new List<string>();

        for (var baseType = type.BaseType;
             baseType is not null && baseType.SpecialType != SpecialType.System_Object;
             baseType = baseType.BaseType)
        {
            names.Add(SymbolFormatting.BuildQualifiedName(baseType));
        }

        return names;
    }

    private static string[] OrderedNames(IEnumerable<ISymbol> symbols)
        => symbols
            .Where(symbol => symbol is INamedTypeSymbol)
            .Select(SymbolFormatting.BuildQualifiedName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
}
