using System.Text.Json;

namespace RoslynGate.E2E.Tests;

internal static class ToolTestHelpers
{
    public static bool IsError(JsonDocument response)
        => response.RootElement
            .GetProperty("result")
            .TryGetProperty("isError", out var isError)
            && isError.GetBoolean();

    public static JsonElement ToolText(JsonDocument response)
    {
        var content = response.RootElement.GetProperty("result").GetProperty("content");
        var text = content[0].GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }
}
