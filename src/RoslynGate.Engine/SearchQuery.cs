using Microsoft.CodeAnalysis;

namespace RoslynGate.Engine;

public static class SearchQuery
{
    public static async Task<SearchResult> GetAsync(
        WorkspaceSnapshot snapshot,
        string? query,
        string? kind,
        string? namespaceFilter,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var text = query?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            throw new SearchException(
                SearchErrorCodes.QueryTooShort,
                "Search query must contain at least 2 characters.",
                "Use a fragment such as Widget, Open, or Door.");
        }

        var appliedLimit = Math.Clamp(limit, 1, QueryLimits.MaxSearchLimit);
        var appliedOffset = Math.Max(offset, 0);
        var normalizedKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        var normalizedNamespace = string.IsNullOrWhiteSpace(namespaceFilter)
            ? null
            : namespaceFilter.Trim();

        var matches = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);

        foreach (var project in snapshot.Solution.Projects.OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            var symbols = compilation.GetSymbolsWithName(
                name => name.Contains(text, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.TypeAndMember,
                cancellationToken);

            foreach (var symbol in symbols)
            {
                var definition = SymbolFormatting.ToDefinition(symbol, snapshot);
                if (definition is null || !Matches(definition, normalizedKind, normalizedNamespace))
                {
                    continue;
                }

                matches.TryAdd(SymbolFormatting.DefinitionKey(definition), definition);
            }
        }

        var ordered = matches.Values
            .OrderBy(definition => Rank(definition, text))
            .ThenBy(definition => definition.QualifiedName, StringComparer.Ordinal)
            .ThenBy(definition => definition.Kind, StringComparer.Ordinal)
            .ThenBy(definition => definition.Location.File, StringComparer.Ordinal)
            .ThenBy(definition => definition.Location.Line)
            .ToArray();

        var safeOffset = Math.Min(appliedOffset, ordered.Length);
        var page = ordered.Skip(safeOffset).Take(appliedLimit).ToArray();

        return new SearchResult(
            page,
            ordered.Length,
            safeOffset,
            appliedLimit,
            Truncated: safeOffset + page.Length < ordered.Length);
    }

    private static bool Matches(
        SymbolDefinition definition,
        string? kind,
        string? namespaceFilter)
    {
        if (kind is not null
            && !string.Equals(definition.Kind, kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (namespaceFilter is null)
        {
            return true;
        }

        return string.Equals(definition.Namespace, namespaceFilter, StringComparison.Ordinal)
            || definition.Namespace?.StartsWith(namespaceFilter + ".", StringComparison.Ordinal) == true;
    }

    private static int Rank(SymbolDefinition definition, string query)
    {
        if (string.Equals(definition.Name, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return definition.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }
}
