using System.Text.Json;
using ModelContextProtocol.Protocol;
using RoslynGate.Engine;

namespace RoslynGate.Mcp.Tests;

public sealed class ToolJsonTests
{
    private static readonly SymbolDefinition Symbol = new(
        "Open",
        "SampleSdk.Widget.Open",
        "method",
        "SampleSdk",
        "SampleSdk.Widget",
        "public Task Open(string doorId)",
        "Opens a door.",
        new SymbolLocation("SampleSdk/Widget.cs", 42, 17));

    [Fact]
    public void CompactDefinitionUsesShortKeys()
    {
        var json = ReadText(DefinitionJson.Definition(new DefinitionResult(Symbol, false, []), DetailLevel.Compact));

        Assert.Equal("Open", json.GetProperty("n").GetString());
        Assert.Equal("SampleSdk.Widget.Open", json.GetProperty("q").GetString());
        Assert.Equal("SampleSdk/Widget.cs", json.GetProperty("f").GetString());
        Assert.Equal(42, json.GetProperty("l").GetInt32());
        Assert.False(json.TryGetProperty("namespace", out _));
    }

    [Fact]
    public void VerboseDefinitionIncludesLocationAndNamespace()
    {
        var json = ReadText(DefinitionJson.Definition(new DefinitionResult(Symbol, false, []), DetailLevel.Verbose));

        var symbol = json.GetProperty("symbol");
        Assert.Equal("SampleSdk", symbol.GetProperty("namespace").GetString());
        Assert.Equal(17, symbol.GetProperty("location").GetProperty("column").GetInt32());
    }

    [Fact]
    public void CompactAmbiguityReturnsCandidateArrays()
    {
        var result = new DefinitionResult(null, true, [Symbol, Symbol with { Signature = "public Task Open()" }]);

        var json = ReadText(DefinitionJson.Definition(result, DetailLevel.Compact));

        Assert.True(json.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(2, json.GetProperty("candidates").GetArrayLength());
        Assert.True(json.TryGetProperty("hint", out _));
    }

    [Fact]
    public void CompactReferencesPageIncludesNextOffset()
    {
        var result = new ReferencesResult(
            Symbol,
            false,
            [],
            [new SymbolReference("call", Symbol.Location)],
            20,
            10,
            1,
            true);

        var json = ReadText(ReferencesJson.References(result, DetailLevel.Compact));

        Assert.Equal(11, json.GetProperty("nextOffset").GetInt32());
        Assert.Contains(
            "find_symbol_references",
            json.GetProperty("hint").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void VerboseSearchPageIncludesNextOffset()
    {
        var result = new SearchResult([Symbol], 20, 10, 1, true);

        var json = ReadText(SearchJson.Search(result, DetailLevel.Verbose));

        Assert.Equal(11, json.GetProperty("nextOffset").GetInt32());
        Assert.Contains(
            "search_symbols",
            json.GetProperty("hint").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorsSetIsErrorAndCarryHint()
    {
        var result = ToolJson.Error("invalid_target", "No target.", "Pass name or file.");
        var json = ReadText(result);

        Assert.True(result.IsError);
        Assert.Equal("invalid_target", json.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Pass name or file.", json.GetProperty("error").GetProperty("hint").GetString());
    }

    [Fact]
    public void ErrorWithSuggestionIncludesTry()
    {
        var result = ToolJson.Error(new ToolError(
            "query_too_short",
            "Query is too short.",
            "Use at least 2 characters.",
            new ToolSuggestion(
                ToolNames.SearchSymbols,
                new Dictionary<string, object?> { ["query"] = "Widget" })));

        var json = ReadText(result);
        var suggestion = json.GetProperty("error").GetProperty("try");

        Assert.Equal(ToolNames.SearchSymbols, suggestion.GetProperty("tool").GetString());
        Assert.Equal("Widget", suggestion.GetProperty("arguments").GetProperty("query").GetString());
    }

    [Fact]
    public void CompactDocumentationIsTruncated()
    {
        var symbol = Symbol with { Documentation = new string('x', 500) };

        var json = ReadText(DefinitionJson.Definition(new DefinitionResult(symbol, false, []), DetailLevel.Compact));
        var documentation = json.GetProperty("d").GetString();

        Assert.NotNull(documentation);
        Assert.True(documentation.Length <= OutputLimits.CompactDocumentationChars + 3);
        Assert.EndsWith("...", documentation, StringComparison.Ordinal);
    }

    private static JsonElement ReadText(CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
