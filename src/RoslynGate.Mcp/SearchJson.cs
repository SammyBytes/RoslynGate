using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class SearchJson
{
    public static CallToolResult Search(SearchResult result, DetailLevel detail)
    {
        var json = detail == DetailLevel.Verbose
            ? WriteVerbose(result)
            : WriteCompact(result);

        return ToolJson.Text(json);
    }

    private static string WriteCompact(SearchResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("total", result.Total);
            writer.WriteNumber("offset", result.Offset);
            writer.WriteBoolean("truncated", result.Truncated);
            writer.WriteStartArray("hits");
            foreach (var hit in result.Hits)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(hit.QualifiedName);
                writer.WriteStringValue(hit.Kind);
                writer.WriteStringValue(hit.Location.File);
                writer.WriteNumberValue(hit.Location.Line);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();

            if (result.Truncated)
            {
                ToolJson.WriteNextPage(
                    writer,
                    result.Offset + result.Hits.Count,
                    ToolNames.SearchSymbols);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteVerbose(SearchResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("total", result.Total);
            writer.WriteNumber("offset", result.Offset);
            writer.WriteNumber("limit", result.Limit);
            writer.WriteBoolean("truncated", result.Truncated);
            writer.WriteStartArray("hits");
            foreach (var hit in result.Hits)
            {
                ToolJson.WriteVerboseSymbol(writer, hit);
            }

            writer.WriteEndArray();

            if (result.Truncated)
            {
                ToolJson.WriteNextPage(
                    writer,
                    result.Offset + result.Hits.Count,
                    ToolNames.SearchSymbols);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
