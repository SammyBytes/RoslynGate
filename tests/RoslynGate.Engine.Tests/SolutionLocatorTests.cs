using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

public sealed class SolutionLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "roslyngate-tests",
        Guid.NewGuid().ToString("N"));

    public SolutionLocatorTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void FindsSingleSlnInDirectory()
    {
        var expected = Path.Combine(_root, "Sample.sln");
        File.WriteAllText(expected, string.Empty);

        var actual = SolutionLocator.Locate(_root, _root);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FindsSingleSlnxInDirectory()
    {
        var expected = Path.Combine(_root, "Sample.slnx");
        File.WriteAllText(expected, string.Empty);

        var actual = SolutionLocator.Locate(_root, _root);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolvesRelativePathFromWorkingDirectory()
    {
        var expected = Path.Combine(_root, "Sample.sln");
        File.WriteAllText(expected, string.Empty);

        var actual = SolutionLocator.Locate("Sample.sln", _root);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FailsWhenDirectoryHasNoSolution()
    {
        var exception = Assert.Throws<SolutionNotFoundException>(
            () => SolutionLocator.Locate(_root, _root));

        Assert.Contains("No .sln or .slnx", exception.Message);
    }

    [Fact]
    public void ListsCandidatesWhenDirectoryHasMultipleSolutions()
    {
        File.WriteAllText(Path.Combine(_root, "A.sln"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "B.slnx"), string.Empty);

        var exception = Assert.Throws<SolutionNotFoundException>(
            () => SolutionLocator.Locate(_root, _root));

        Assert.Contains("A.sln", exception.Message);
        Assert.Contains("B.slnx", exception.Message);
    }

    [Fact]
    public void RejectsFileThatIsNotASolution()
    {
        var path = Path.Combine(_root, "notes.txt");
        File.WriteAllText(path, string.Empty);

        var exception = Assert.Throws<SolutionNotFoundException>(
            () => SolutionLocator.Locate(path, _root));

        Assert.Contains("Not a solution file", exception.Message);
    }

    [Fact]
    public void FailsWhenExplicitPathDoesNotExist()
    {
        var missing = Path.Combine(_root, "missing.sln");

        var exception = Assert.Throws<SolutionNotFoundException>(
            () => SolutionLocator.Locate(missing, _root));

        Assert.Contains("does not exist", exception.Message);
    }
}
