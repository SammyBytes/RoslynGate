using System.Text.Json;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static partial class ToolJson
{
    internal static void WriteCompactCandidate(Utf8JsonWriter writer, SymbolDefinition symbol)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(symbol.QualifiedName);
        writer.WriteStringValue(symbol.Kind);
        writer.WriteStringValue(symbol.Signature);
        writer.WriteStringValue(symbol.Location.File);
        writer.WriteNumberValue(symbol.Location.Line);
        writer.WriteEndArray();
    }

    internal static void WriteCompactSymbol(Utf8JsonWriter writer, SymbolDefinition symbol)
    {
        writer.WriteStartObject();
        writer.WriteString("n", symbol.Name);
        writer.WriteString("q", symbol.QualifiedName);
        writer.WriteString("k", symbol.Kind);
        writer.WriteString("s", symbol.Signature);

        if (symbol.Documentation is { Length: > 0 } documentation)
        {
            writer.WriteString(
                "d",
                Truncate(documentation, OutputLimits.CompactDocumentationChars));
        }

        writer.WriteString("f", symbol.Location.File);
        writer.WriteNumber("l", symbol.Location.Line);
        writer.WriteEndObject();
    }

    internal static void WriteVerboseSymbol(Utf8JsonWriter writer, SymbolDefinition symbol)
    {
        writer.WriteStartObject();
        writer.WriteString("name", symbol.Name);
        writer.WriteString("qualifiedName", symbol.QualifiedName);
        writer.WriteString("kind", symbol.Kind);

        if (symbol.Namespace is not null)
        {
            writer.WriteString("namespace", symbol.Namespace);
        }

        if (symbol.ContainingType is not null)
        {
            writer.WriteString("containingType", symbol.ContainingType);
        }

        writer.WriteString("signature", symbol.Signature);

        if (symbol.Documentation is { Length: > 0 } documentation)
        {
            writer.WriteString(
                "documentation",
                Truncate(documentation, OutputLimits.VerboseDocumentationChars));
        }

        writer.WriteStartObject("location");
        writer.WriteString("file", symbol.Location.File);
        writer.WriteNumber("line", symbol.Location.Line);
        writer.WriteNumber("column", symbol.Location.Column);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
