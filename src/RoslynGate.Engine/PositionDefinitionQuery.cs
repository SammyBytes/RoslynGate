using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace RoslynGate.Engine;

internal static class PositionDefinitionQuery
{
    public static async Task<DefinitionResult> GetAsync(
        WorkspaceSnapshot snapshot,
        SymbolTarget target,
        CancellationToken cancellationToken)
    {
        if (target.Line is null)
        {
            throw new DefinitionException(
                DefinitionErrorCodes.PositionIncomplete,
                "A file was provided without a line.",
                "Pass line with a 1-based line number, or use name.");
        }

        var requestedLine = target.Line.Value;
        if (requestedLine < 1)
        {
            throw new DefinitionException(
                DefinitionErrorCodes.LineOutOfRange,
                $"Line {requestedLine} is not valid.",
                "Lines are 1-based.");
        }

        var fullPath = ResolvePath(snapshot, target.File!);
        if (!File.Exists(fullPath))
        {
            throw new DefinitionException(
                DefinitionErrorCodes.FileNotFound,
                $"File not found: {fullPath}.",
                "Paths are relative to the solution root. Use a path inside the solution.");
        }

        var document = snapshot.Solution.Projects
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .SelectMany(project => project.Documents)
            .FirstOrDefault(candidate =>
                candidate.FilePath is not null
                && PathsEqual(candidate.FilePath, fullPath));

        if (document is null)
        {
            throw new DefinitionException(
                DefinitionErrorCodes.FileOutsideSolution,
                $"File is not part of the loaded solution: {fullPath}.",
                "Use a file that belongs to the solution.");
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (requestedLine > text.Lines.Count)
        {
            throw new DefinitionException(
                DefinitionErrorCodes.LineOutOfRange,
                $"Line {requestedLine} is outside {fullPath} ({text.Lines.Count} lines).",
                $"Use a line between 1 and {text.Lines.Count}.");
        }

        var line = text.Lines[requestedLine - 1];
        var column = Math.Max(target.Column ?? 1, 1);
        var offset = line.Start + Math.Min(column - 1, line.Span.Length);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return DefinitionQuery.CreateResult([]);
        }

        var token = root.FindToken(offset);
        var symbols = FindSymbols(semanticModel, token, cancellationToken);
        var definitions = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);

        foreach (var symbol in symbols)
        {
            var definition = SymbolFormatting.ToDefinition(symbol, snapshot);
            if (definition is not null)
            {
                definitions.TryAdd(SymbolFormatting.DefinitionKey(definition), definition);
            }
        }

        return DefinitionQuery.CreateResult(definitions.Values);
    }

    private static ImmutableArray<ISymbol> FindSymbols(
        SemanticModel semanticModel,
        SyntaxToken token,
        CancellationToken cancellationToken)
    {
        var node = token.Parent;
        while (node is not null)
        {
            var declared = semanticModel.GetDeclaredSymbol(node, cancellationToken);
            if (declared is not null)
            {
                return [declared];
            }

            var info = semanticModel.GetSymbolInfo(node, cancellationToken);
            if (info.Symbol is not null)
            {
                return [info.Symbol];
            }

            if (info.CandidateSymbols.Length > 0)
            {
                return info.CandidateSymbols;
            }

            node = node.Parent;
        }

        return [];
    }

    private static string ResolvePath(WorkspaceSnapshot snapshot, string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, snapshot.RootPath);

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.Ordinal);
}
