using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class ToolJson
{
    private const string AmbiguousHint = "Use file + line from one candidate.";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static CallToolResult Definition(DefinitionResult result, DetailLevel detail)
    {
        var json = detail == DetailLevel.Verbose
            ? WriteVerbose(result)
            : WriteCompact(result);

        return Text(json);
    }

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

    private static string WriteCompact(DefinitionResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            if (result.Symbol is not null)
            {
                WriteCompactSymbol(writer, result.Symbol);
            }
            else if (result.Ambiguous)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("ambiguous", true);
                writer.WriteStartArray("candidates");
                foreach (var candidate in result.Candidates)
                {
                    WriteCompactCandidate(writer, candidate);
                }

                writer.WriteEndArray();
                writer.WriteString("hint", AmbiguousHint);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteNull("symbol");
                writer.WriteEndObject();
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteVerbose(DefinitionResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();

            if (result.Symbol is not null)
            {
                writer.WritePropertyName("symbol");
                WriteVerboseSymbol(writer, result.Symbol);
            }
            else
            {
                writer.WriteNull("symbol");
            }

            writer.WriteBoolean("ambiguous", result.Ambiguous);

            if (result.Ambiguous)
            {
                writer.WriteStartArray("candidates");
                foreach (var candidate in result.Candidates)
                {
                    WriteVerboseSymbol(writer, candidate);
                }

                writer.WriteEndArray();
                writer.WriteString("hint", AmbiguousHint);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCompactSymbol(Utf8JsonWriter writer, SymbolDefinition symbol)
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

    private static void WriteCompactCandidate(Utf8JsonWriter writer, SymbolDefinition symbol)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(symbol.QualifiedName);
        writer.WriteStringValue(symbol.Kind);
        writer.WriteStringValue(symbol.Signature);
        writer.WriteStringValue(symbol.Location.File);
        writer.WriteNumberValue(symbol.Location.Line);
        writer.WriteEndArray();
    }

    private static void WriteVerboseSymbol(Utf8JsonWriter writer, SymbolDefinition symbol)
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

    private static string Truncate(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum] + "...";

    private static CallToolResult Text(string json, bool isError = false)
        => new()
        {
            IsError = isError,
            Content = [new TextContentBlock { Text = json }]
        };
}
