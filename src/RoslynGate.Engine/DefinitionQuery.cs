using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace RoslynGate.Engine;

public static class DefinitionQuery
{
    public static async Task<DefinitionResult> GetAsync(
        WorkspaceSnapshot snapshot,
        SymbolTarget target,
        CancellationToken cancellationToken)
    {
        if (target.File is not null)
        {
            return await PositionDefinitionQuery
                .GetAsync(snapshot, target, cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(target.Name))
        {
            throw new DefinitionException(
                DefinitionErrorCodes.InvalidTarget,
                "No symbol name or position was provided.",
                "Pass name, or file with a 1-based line.");
        }

        return await FromNameAsync(snapshot, target.Name, cancellationToken).ConfigureAwait(false);
    }

    internal static DefinitionResult CreateResult(IEnumerable<SymbolDefinition> definitions)
    {
        var ordered = definitions
            .OrderBy(definition => definition.QualifiedName, StringComparer.Ordinal)
            .ThenBy(definition => definition.Signature, StringComparer.Ordinal)
            .ThenBy(definition => definition.Location.File, StringComparer.Ordinal)
            .ThenBy(definition => definition.Location.Line)
            .ThenBy(definition => definition.Location.Column)
            .ToArray();

        return ordered.Length switch
        {
            0 => new DefinitionResult(null, Ambiguous: false, Array.Empty<SymbolDefinition>()),
            1 => new DefinitionResult(ordered[0], Ambiguous: false, Array.Empty<SymbolDefinition>()),
            _ => new DefinitionResult(
                null,
                Ambiguous: true,
                ordered.Take(DefinitionLimits.MaxCandidates).ToArray())
        };
    }

    private static async Task<DefinitionResult> FromNameAsync(
        WorkspaceSnapshot snapshot,
        string targetName,
        CancellationToken cancellationToken)
    {
        var name = SymbolFormatting.NormalizeName(targetName);
        var simpleName = name[(name.LastIndexOf('.') + 1)..];

        if (simpleName.Length == 0)
        {
            throw new DefinitionException(
                DefinitionErrorCodes.InvalidTarget,
                $"Invalid symbol name: '{targetName}'.",
                "Use a name such as SampleSdk.Widget.Open.");
        }

        var matches = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);

        foreach (var project in snapshot.Solution.Projects.OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            var declarations = await SymbolFinder
                .FindDeclarationsAsync(
                    project,
                    simpleName,
                    ignoreCase: false,
                    SymbolFilter.TypeAndMember,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var symbol in declarations)
            {
                if (!MatchesTarget(symbol, name))
                {
                    continue;
                }

                var definition = SymbolFormatting.ToDefinition(symbol, snapshot);
                if (definition is not null)
                {
                    matches.TryAdd(SymbolFormatting.DefinitionKey(definition), definition);
                }
            }
        }

        return CreateResult(matches.Values);
    }

    private static bool MatchesTarget(ISymbol symbol, string target)
    {
        var qualifiedName = SymbolFormatting.BuildQualifiedName(symbol);

        if (string.Equals(qualifiedName, target, StringComparison.Ordinal))
        {
            return true;
        }

        if (!target.Contains('.', StringComparison.Ordinal))
        {
            return string.Equals(symbol.Name, target, StringComparison.Ordinal);
        }

        return qualifiedName.EndsWith("." + target, StringComparison.Ordinal);
    }
}
