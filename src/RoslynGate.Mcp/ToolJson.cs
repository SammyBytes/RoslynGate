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
        => Error(new ToolError(code, message, hint));

    public static CallToolResult Error(ToolError error)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("error");
            writer.WriteString("code", error.Code);
            writer.WriteString("message", error.Message);
            writer.WriteString("hint", error.Hint);

            if (error.Try is not null)
            {
                WriteTry(writer, error.Try);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Text(Encoding.UTF8.GetString(stream.ToArray()), isError: true);
    }

    internal static void WriteNotFound(
        Utf8JsonWriter writer,
        string property,
        string? searchQuery)
    {
        writer.WriteStartObject();
        writer.WriteNull(property);

        if (searchQuery is not null)
        {
            WriteSuggestion(writer, searchQuery);
        }

        writer.WriteEndObject();
    }

    internal static void WriteSuggestion(Utf8JsonWriter writer, string searchQuery)
    {
        writer.WriteString("hint", "Use search_symbols to find a fragment.");
        WriteTry(
            writer,
            new ToolSuggestion(
                ToolNames.SearchSymbols,
                new Dictionary<string, object?> { ["query"] = searchQuery }));
    }

    internal static void WriteNextPage(
        Utf8JsonWriter writer,
        int nextOffset,
        string tool)
    {
        writer.WriteNumber("nextOffset", nextOffset);
        writer.WriteString(
            "hint",
            $"Call {tool} again with offset={nextOffset} and the same arguments.");
    }

    internal static string? SearchQueryFor(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        var parenthesis = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (parenthesis >= 0)
        {
            trimmed = trimmed[..parenthesis];
        }

        var simpleName = trimmed[(trimmed.LastIndexOf('.') + 1)..];
        return simpleName.Length >= 2 ? simpleName : null;
    }

    private static void WriteTry(Utf8JsonWriter writer, ToolSuggestion suggestion)
    {
        writer.WriteStartObject("try");
        writer.WriteString("tool", suggestion.Tool);
        writer.WriteStartObject("arguments");

        foreach (var (key, value) in suggestion.Arguments)
        {
            writer.WritePropertyName(key);
            WriteArgumentValue(writer, value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteArgumentValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
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
