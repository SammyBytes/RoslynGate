namespace RoslynGate.Engine;

public static class SolutionLocator
{
    public static string Locate(string? path, string workingDirectory)
    {
        if (path is not null)
        {
            var fullPath = Path.GetFullPath(path, workingDirectory);
            if (File.Exists(fullPath))
            {
                return EnsureSolutionFile(fullPath);
            }

            if (Directory.Exists(fullPath))
            {
                return LocateIn(fullPath);
            }

            throw new SolutionNotFoundException($"Solution path does not exist: {fullPath}");
        }

        return LocateIn(workingDirectory);
    }

    private static string LocateIn(string directory)
    {
        var candidates = Directory
            .EnumerateFiles(directory)
            .Where(IsSolutionFile)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        return candidates.Length switch
        {
            0 => throw new SolutionNotFoundException(
                $"No .sln or .slnx found in {directory}. Pass --solution <path>."),
            1 => candidates[0],
            _ => throw new SolutionNotFoundException(
                $"Multiple solutions found in {directory}:{Environment.NewLine}" +
                string.Join(Environment.NewLine, candidates.Select(path => $"  {path}")) +
                $"{Environment.NewLine}Pass --solution <path> to choose one.")
        };
    }

    private static string EnsureSolutionFile(string path)
    {
        if (!IsSolutionFile(path))
        {
            throw new SolutionNotFoundException(
                $"Not a solution file: {path}. Expected a .sln or .slnx file.");
        }

        return path;
    }

    private static bool IsSolutionFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }
}
