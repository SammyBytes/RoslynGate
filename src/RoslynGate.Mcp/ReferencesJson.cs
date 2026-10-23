using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class ReferencesJson
{
    public static CallToolResult References(
        ReferencesResult result,
        DetailLevel detail,
        string? searchQuery = null,
        bool includeDefinition = false)
    {
        var json = detail == DetailLevel.Verbose
            ? WriteVerbose(result, searchQuery)
            : WriteCompact(result, searchQuery, includeDefinition);

        return ToolJson.Text(json);
    }

    private static string WriteCompact(
        ReferencesResult result,
        string? searchQuery,
        bool includeDefinition)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            if (result.Symbol is null && !result.Ambiguous)
            {
                ToolJson.WriteNotFound(writer, "symbol", searchQuery);
            }
            else if (result.Ambiguous)
            {
                WriteAmbiguous(writer, result.Candidates);
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("q", result.Symbol!.QualifiedName);

                if (includeDefinition)
                {
                    writer.WritePropertyName("definition");
                    ToolJson.WriteCompactSymbol(writer, result.Symbol);
                }

                writer.WriteNumber("total", result.Total);
                writer.WriteNumber("offset", result.Offset);
                writer.WriteBoolean("truncated", result.Truncated);
                writer.WriteStartArray("refs");
                foreach (var reference in result.References)
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(reference.Location.File);
                    writer.WriteNumberValue(reference.Location.Line);
                    writer.WriteStringValue(reference.Kind);
                    writer.WriteEndArray();
                }

                writer.WriteEndArray();

                if (result.Truncated)
                {
                    ToolJson.WriteNextPage(
                        writer,
                        result.Offset + result.References.Count,
                        ToolNames.FindSymbolReferences);
                }

                writer.WriteEndObject();
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteVerbose(ReferencesResult result, string? searchQuery)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            writer.WriteStartObject();

            if (result.Symbol is null)
            {
                writer.WriteNull("symbol");
                if (searchQuery is not null)
                {
                    ToolJson.WriteSuggestion(writer, searchQuery);
                }
            }
            else
            {
                writer.WritePropertyName("symbol");
                ToolJson.WriteVerboseSymbol(writer, result.Symbol);
            }

            writer.WriteBoolean("ambiguous", result.Ambiguous);
            writer.WriteNumber("total", result.Total);
            writer.WriteNumber("offset", result.Offset);
            writer.WriteNumber("limit", result.Limit);
            writer.WriteBoolean("truncated", result.Truncated);

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
            else
            {
                writer.WriteStartArray("references");
                foreach (var reference in result.References)
                {
                    writer.WriteStartObject();
                    writer.WriteString("kind", reference.Kind);
                    writer.WriteString("file", reference.Location.File);
                    writer.WriteNumber("line", reference.Location.Line);
                    writer.WriteNumber("column", reference.Location.Column);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();

                if (result.Truncated)
                {
                    ToolJson.WriteNextPage(
                        writer,
                        result.Offset + result.References.Count,
                        ToolNames.FindSymbolReferences);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
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
