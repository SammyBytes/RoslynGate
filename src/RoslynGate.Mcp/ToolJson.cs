using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class ToolJson
{
    internal const string AmbiguousHint = "Use file + line from one candidate.";

    internal static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static CallToolResult Error(string code, string message, string hint)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("error");
            writer.WriteString("code", code);
            writer.WriteString("message", message);
            writer.WriteString("hint", hint);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Text(Encoding.UTF8.GetString(stream.ToArray()), isError: true);
    }

    internal static CallToolResult Text(string json, bool isError = false)
        => new()
        {
            IsError = isError,
            Content = [new TextContentBlock { Text = json }]
        };

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

    internal static string Truncate(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum] + "...";
}
