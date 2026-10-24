using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace RoslynGate.Engine;

public static class SymbolResolver
{
    public static async Task<SymbolResolution> ResolveAsync(
        WorkspaceSnapshot snapshot,
        SymbolTarget target,
        CancellationToken cancellationToken)
    {
        if (target.File is not null)
        {
            return await PositionSymbolResolver
                .ResolveAsync(snapshot, target, cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(target.Name))
        {
            throw new DefinitionException(
                DefinitionErrorCodes.InvalidTarget,
                "No symbol name or position was provided.",
                "Pass name, or file with a 1-based line.");
        }

        var resolution = await FromNameAsync(snapshot, target.Name, cancellationToken).ConfigureAwait(false);
        return target.ParameterTypes is { Count: > 0 }
            ? FilterByParameterTypes(resolution, target.ParameterTypes)
            : resolution;
    }

    private static SymbolResolution FilterByParameterTypes(
        SymbolResolution resolution,
        IReadOnlyList<string> parameterTypes)
    {
        var filtered = resolution.Matches
            .Where(match => match.Symbol is IMethodSymbol method
                && MatchesParameters(method, parameterTypes))
            .ToArray();

        return new SymbolResolution(filtered);
    }

    private static bool MatchesParameters(IMethodSymbol method, IReadOnlyList<string> expected)
    {
        if (method.Parameters.Length != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index += 1)
        {
            var actual = method.Parameters[index].Type;
            var name = expected[index].Trim();
            if (!string.Equals(name, actual.Name, StringComparison.Ordinal)
                && !string.Equals(name, actual.ToDisplayString(), StringComparison.Ordinal)
                && !string.Equals(name, actual.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    internal static void AddMatch(
        Dictionary<string, ResolvedSymbol> matches,
        ISymbol symbol,
        WorkspaceSnapshot snapshot)
    {
        var definition = SymbolFormatting.ToDefinition(symbol, snapshot);
        if (definition is not null)
        {
            matches.TryAdd(SymbolFormatting.DefinitionKey(definition), new ResolvedSymbol(symbol, definition));
        }
    }

    internal static SymbolResolution CreateResolution(IEnumerable<ResolvedSymbol> matches)
    {
        var ordered = matches
            .OrderBy(match => match.Definition.QualifiedName, StringComparer.Ordinal)
            .ThenBy(match => match.Definition.Signature, StringComparer.Ordinal)
            .ThenBy(match => match.Definition.Location.File, StringComparer.Ordinal)
            .ThenBy(match => match.Definition.Location.Line)
            .ThenBy(match => match.Definition.Location.Column)
            .ToArray();

        return new SymbolResolution(ordered);
    }

    private static async Task<SymbolResolution> FromNameAsync(
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

        var matches = new Dictionary<string, ResolvedSymbol>(StringComparer.Ordinal);

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
                if (MatchesTarget(symbol, name))
                {
                    AddMatch(matches, symbol, snapshot);
                }
            }
        }

        return CreateResolution(matches.Values);
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
