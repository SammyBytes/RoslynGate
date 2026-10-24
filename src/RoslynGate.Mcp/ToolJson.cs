using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static partial class ToolJson
{
    internal const string AmbiguousHint = "Use parameterTypes to choose an overload, or file + line from one candidate.";

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

    internal static CallToolResult FileNotIndexed(string file)
        => Error(new ToolError(
            "file_not_indexed",
            $"The file is not part of the loaded workspace: {file}.",
            "Call reload_workspace, then retry."));

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

    public static CallToolResult Text(string json, bool isError = false)
        => new()
        {
            IsError = isError,
            Content = [new TextContentBlock { Text = json }]
        };

    public static CallToolResult WithState(CallToolResult result, WorkspaceState state)
    {
        if (result.IsError == true || !state.Stale)
        {
            return result;
        }

        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        if (JsonNode.Parse(text) is not JsonObject root)
        {
            return result;
        }

        var changed = new JsonArray();
        foreach (var path in state.ChangedFiles)
        {
            changed.Add(path);
        }

        root["stale"] = true;
        root["changed"] = changed;
        root["hint"] = "The workspace changed after it was loaded. Call reload_workspace, then retry.";
        root["try"] = new JsonObject
        {
            ["tool"] = ToolNames.ReloadWorkspace,
            ["arguments"] = new JsonObject()
        };

        return Text(root.ToJsonString());
    }

    internal static string Truncate(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum] + "...";
}
