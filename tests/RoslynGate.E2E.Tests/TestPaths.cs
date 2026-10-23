namespace RoslynGate.E2E.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string SampleSolutionDirectory { get; } =
        Path.Combine(RepositoryRoot, "fixtures", "SampleSolution");

    public static string SampleSolution { get; } =
        Path.Combine(SampleSolutionDirectory, "SampleSolution.sln");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RoslynGate.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
