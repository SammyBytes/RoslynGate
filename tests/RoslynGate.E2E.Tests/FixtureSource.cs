namespace RoslynGate.E2E.Tests;

internal static class FixtureSource
{
    public static int LineOf(string relativePath, string text)
    {
        var lines = File.ReadAllLines(Path.Combine(TestPaths.SampleSolutionDirectory, relativePath));
        var index = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        return index < 0
            ? throw new InvalidOperationException($"Could not find '{text}' in {relativePath}.")
            : index + 1;
    }
}
