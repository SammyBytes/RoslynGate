using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace RoslynGate.Engine;

public static class ReferenceQuery
{
    public static async Task<ReferencesResult> GetAsync(
        WorkspaceSnapshot snapshot,
        SymbolTarget target,
        bool includeDeclarations,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var resolution = await SymbolResolver
            .ResolveAsync(snapshot, target, cancellationToken)
            .ConfigureAwait(false);

        var appliedLimit = Math.Clamp(limit, 1, QueryLimits.MaxReferenceLimit);
        var appliedOffset = Math.Max(offset, 0);

        if (resolution.Matches.Count == 0)
        {
            return Empty(appliedOffset, appliedLimit);
        }

        if (resolution.Matches.Count > 1)
        {
            return new ReferencesResult(
                null,
                Ambiguous: true,
                resolution.Matches
                    .Take(DefinitionLimits.MaxCandidates)
                    .Select(match => match.Definition)
                    .ToArray(),
                Array.Empty<SymbolReference>(),
                Total: 0,
                Offset: appliedOffset,
                Limit: appliedLimit,
                Truncated: false);
        }

        var match = resolution.Matches[0];
        var referencedSymbols = await SymbolFinder
            .FindReferencesAsync(match.Symbol, snapshot.Solution, cancellationToken)
            .ConfigureAwait(false);

        var references = new Dictionary<string, SymbolReference>(StringComparer.Ordinal);

        foreach (var referenced in referencedSymbols)
        {
            if (includeDeclarations)
            {
                foreach (var location in referenced.Definition.Locations)
                {
                    AddReference(references, SymbolFormatting.ToLocation(location, snapshot), "declaration");
                }
            }

            foreach (var reference in referenced.Locations)
            {
                var kind = await KindOfAsync(reference, cancellationToken).ConfigureAwait(false);
                AddReference(references, SymbolFormatting.ToLocation(reference.Location, snapshot), kind);
            }
        }

        var ordered = references.Values
            .OrderBy(reference => reference.Location.File, StringComparer.Ordinal)
            .ThenBy(reference => reference.Location.Line)
            .ThenBy(reference => reference.Location.Column)
            .ThenBy(reference => reference.Kind, StringComparer.Ordinal)
            .ToArray();

        var safeOffset = Math.Min(appliedOffset, ordered.Length);
        var page = ordered.Skip(safeOffset).Take(appliedLimit).ToArray();

        return new ReferencesResult(
            match.Definition,
            Ambiguous: false,
            Array.Empty<SymbolDefinition>(),
            page,
            ordered.Length,
            safeOffset,
            appliedLimit,
            Truncated: safeOffset + page.Length < ordered.Length);
    }

    private static ReferencesResult Empty(int offset, int limit)
        => new(
            null,
            Ambiguous: false,
            Array.Empty<SymbolDefinition>(),
            Array.Empty<SymbolReference>(),
            Total: 0,
            offset,
            limit,
            Truncated: false);

    private static void AddReference(
        Dictionary<string, SymbolReference> references,
        SymbolLocation? location,
        string kind)
    {
        if (location is null)
        {
            return;
        }

        var key = $"{location.File}|{location.Line}|{location.Column}|{kind}";
        references.TryAdd(key, new SymbolReference(kind, location));
    }

    private static async Task<string> KindOfAsync(
        ReferenceLocation reference,
        CancellationToken cancellationToken)
    {
        if (reference.IsImplicit)
        {
            return "implicit";
        }

        var root = await reference.Document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return "read";
        }

        var span = reference.Location.SourceSpan;
        var node = root.FindNode(span);

        if (node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression.Span.Contains(span)))
        {
            return "call";
        }

        if (node.AncestorsAndSelf().OfType<BaseListSyntax>().Any())
        {
            return "inherit";
        }

        return "read";
    }
}
