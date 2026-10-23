using System.Text.Json;

namespace RoslynGate.Engine;

internal static class GlobalJson
{
    public static string? Find(string workingDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(workingDirectory));
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "global.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string? ReadRequestedVersion(string globalJson)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(globalJson));
            return document.RootElement.TryGetProperty("sdk", out var sdk)
                && sdk.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.String
                    ? version.GetString()
                    : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
