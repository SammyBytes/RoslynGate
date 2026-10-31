namespace RoslynGate.Cli;

internal static class ProjectKind
{
    public const string TypeScriptPackage = "roslyngate-ts";

    private static readonly string[] TypeScriptConfigs = ["tsconfig.json", "jsconfig.json"];

    private static readonly string[] TypeScriptExtensions =
        [".ts", ".tsx", ".js", ".jsx", ".mts", ".cts", ".mjs", ".cjs"];

    public static bool IsTypeScriptPath(string path)
    {
        var name = Path.GetFileName(path);
        if (TypeScriptConfigs.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return TypeScriptExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);
    }

    public static string? FindTypeScriptConfig(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return TypeScriptConfigs
            .Select(name => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    public static bool HasDotNetSolution(string directory)
        => Directory.Exists(directory)
            && Directory.EnumerateFiles(directory)
                .Any(path =>
                {
                    var extension = Path.GetExtension(path);
                    return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
                });

    public static string Message(string detail)
        => $"This looks like a TypeScript workspace ({detail}). roslyngate only serves .NET solutions.";

    public static string Hint
        => $"Use the TypeScript package instead: npx -y {TypeScriptPackage} --solution <path>.";
}
