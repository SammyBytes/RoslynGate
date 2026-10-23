using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class DefinitionJson
{
    public static CallToolResult Definition(DefinitionResult result, DetailLevel detail)
    {
        var json = detail == DetailLevel.Verbose
            ? WriteVerbose(result)
            : WriteCompact(result);

        return ToolJson.Text(json);
    }

    private static string WriteCompact(DefinitionResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            if (result.Symbol is not null)
            {
                WriteCompactSymbol(writer, result.Symbol);
            }
            else if (result.Ambiguous)
            {
                WriteAmbiguous(writer, result.Candidates);
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
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            writer.WriteStartObject();

            if (result.Symbol is not null)
            {
                writer.WritePropertyName("symbol");
                ToolJson.WriteVerboseSymbol(writer, result.Symbol);
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
                    ToolJson.WriteVerboseSymbol(writer, candidate);
                }

                writer.WriteEndArray();
                writer.WriteString("hint", ToolJson.AmbiguousHint);
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
                ToolJson.Truncate(documentation, OutputLimits.CompactDocumentationChars));
        }

        writer.WriteString("f", symbol.Location.File);
        writer.WriteNumber("l", symbol.Location.Line);
        writer.WriteEndObject();
    }

    private static void WriteAmbiguous(
        Utf8JsonWriter writer,
        IReadOnlyList<SymbolDefinition> candidates)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("ambiguous", true);
        writer.WriteStartArray("candidates");
        foreach (var candidate in candidates)
        {
            ToolJson.WriteCompactCandidate(writer, candidate);
        }

        writer.WriteEndArray();
        writer.WriteString("hint", ToolJson.AmbiguousHint);
        writer.WriteEndObject();
    }
}
