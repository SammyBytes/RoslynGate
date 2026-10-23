using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

public sealed class DotNetSdkLocatorTests
{
    [Fact]
    public void MissingPinnedSdkReportsGlobalJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyngate-sdk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "global.json"),
            """{"sdk":{"version":"0.0.1"}}""");

        try
        {
            var exception = Assert.Throws<WorkspaceLoadException>(
                () => DotNetSdkLocator.Find(root));

            Assert.Contains("global.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("0.0.1", exception.Hint, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
