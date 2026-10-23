using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace RoslynGate.Engine;

internal static class SymbolFormatting
{
    private static readonly SymbolDisplayFormat SignatureFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue
            | SymbolDisplayParameterOptions.IncludeModifiers,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        var parenthesis = normalized.IndexOf('(', StringComparison.Ordinal);
        if (parenthesis >= 0)
        {
            normalized = normalized[..parenthesis];
        }

        return normalized.Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    public static string BuildQualifiedName(ISymbol symbol)
    {
        var parts = new List<string>();

        if (symbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            parts.Add(ns.ToDisplayString());
        }

        var types = new Stack<string>();
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            types.Push(type.Name);
        }

        parts.AddRange(types);
        parts.Add(symbol.Name);
        return string.Join('.', parts);
    }

    public static SymbolDefinition? ToDefinition(ISymbol symbol, WorkspaceSnapshot snapshot)
    {
        if (symbol is INamespaceSymbol || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        var location = symbol.Locations
            .Where(candidate => candidate.IsInSource && candidate.SourceTree is not null)
            .OrderBy(candidate => candidate.SourceTree!.FilePath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.GetLineSpan().StartLinePosition.Line)
            .ThenBy(candidate => candidate.GetLineSpan().StartLinePosition.Character)
            .FirstOrDefault();

        if (location is null)
        {
            return null;
        }

        var symbolLocation = ToLocation(location, snapshot);
        if (symbolLocation is null)
        {
            return null;
        }

        return new SymbolDefinition(
            symbol.Name,
            BuildQualifiedName(symbol),
            KindOf(symbol),
            NamespaceOf(symbol),
            ContainingTypeOf(symbol),
            symbol.ToDisplayString(SignatureFormat),
            DocumentationOf(symbol),
            symbolLocation);
    }

    public static SymbolLocation? ToLocation(Location location, WorkspaceSnapshot snapshot)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var span = location.GetLineSpan();
        var fullPath = Path.GetFullPath(span.Path, snapshot.RootPath);
        var relativePath = Path.GetRelativePath(snapshot.RootPath, fullPath).Replace('\\', '/');

        return new SymbolLocation(
            relativePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }

    public static string DefinitionKey(SymbolDefinition definition)
        => $"{definition.QualifiedName}|{definition.Signature}|{definition.Location.File}|" +
           $"{definition.Location.Line}|{definition.Location.Column}";

    private static string? NamespaceOf(ISymbol symbol)
        => symbol.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : null;

    private static string? ContainingTypeOf(ISymbol symbol)
        => symbol.ContainingType is null
            ? null
            : BuildQualifiedName(symbol.ContainingType);

    private static string KindOf(ISymbol symbol)
        => symbol is INamedTypeSymbol named
            ? named.TypeKind.ToString().ToLowerInvariant()
            : symbol.Kind.ToString().ToLowerInvariant();

    private static string? DocumentationOf(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var element = XElement.Parse(xml);
            var text = string.Join(
                " ",
                element.DescendantNodes().OfType<XText>().Select(node => node.Value));
            return Collapse(text);
        }
        catch (XmlException)
        {
            return Collapse(Regex.Replace(xml, "<[^>]+>", " "));
        }
    }

    private static string Collapse(string text)
        => Regex.Replace(text, @"\s+", " ").Trim();
}
