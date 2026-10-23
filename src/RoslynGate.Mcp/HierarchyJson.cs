using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class HierarchyJson
{
    public static CallToolResult Hierarchy(HierarchyResult result, DetailLevel detail)
    {
        var json = detail == DetailLevel.Verbose
            ? WriteVerbose(result)
            : WriteCompact(result);

        return ToolJson.Text(json);
    }

    private static string WriteCompact(HierarchyResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            if (result.Type is null && !result.Ambiguous)
            {
                writer.WriteStartObject();
                writer.WriteNull("type");
                writer.WriteEndObject();
            }
            else if (result.Ambiguous)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("ambiguous", true);
                writer.WriteStartArray("candidates");
                foreach (var candidate in result.Candidates)
                {
                    ToolJson.WriteCompactCandidate(writer, candidate);
                }

                writer.WriteEndArray();
                writer.WriteString("hint", ToolJson.AmbiguousHint);
                writer.WriteEndObject();
            }
            else
            {
                var hierarchy = result.Type!;
                writer.WriteStartObject();
                writer.WriteString("t", hierarchy.Type.QualifiedName);
                WriteArray(writer, "b", hierarchy.BaseTypes);
                WriteArray(writer, "i", hierarchy.Interfaces);
                WriteArray(writer, "d", hierarchy.DerivedTypes);
                WriteArray(writer, "m", hierarchy.Implementations);
                writer.WriteEndObject();
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteVerbose(HierarchyResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ToolJson.WriterOptions))
        {
            writer.WriteStartObject();

            if (result.Type is null)
            {
                writer.WriteNull("type");
            }
            else
            {
                writer.WritePropertyName("type");
                ToolJson.WriteVerboseSymbol(writer, result.Type.Type);
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
            else if (result.Type is not null)
            {
                WriteVerboseArray(writer, "baseTypes", result.Type.BaseTypes);
                WriteVerboseArray(writer, "interfaces", result.Type.Interfaces);
                WriteVerboseArray(writer, "derivedTypes", result.Type.DerivedTypes);
                WriteVerboseArray(writer, "implementations", result.Type.Implementations);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteVerboseArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
