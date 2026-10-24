using RoslynGate.Engine;

namespace RoslynGate.Engine.Tests;

public sealed class WorkspaceFreshnessTests
{
    [Fact]
    public async Task WatcherAddsANewMethodWithoutReloading()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyngate-watch-{Guid.NewGuid():N}");
        CopyDirectory(TestPaths.SampleSolutionDirectory, root);

        try
        {
            using var workspace = new Workspace(Path.Combine(root, "SampleSolution.sln"));
            var initial = await workspace.GetAsync(CancellationToken.None);
            Assert.NotNull(initial);

            var widget = Path.Combine(root, "SampleSdk", "Widget.cs");
            var source = await File.ReadAllTextAsync(widget);
            var close = source.LastIndexOf('}');
            await File.WriteAllTextAsync(
                widget,
                source[..close] + "\n    public int FreshValue => 42;\n}\n");

            DefinitionResult? found = null;
            for (var attempt = 0; attempt < 50 && found?.Symbol is null; attempt += 1)
            {
                await Task.Delay(100);
                var snapshot = await workspace.GetAsync(CancellationToken.None);
                found = await DefinitionQuery.GetAsync(
                    snapshot,
                    new SymbolTarget("SampleSdk.Widget.FreshValue", null, null, null),
                    CancellationToken.None);
            }

            Assert.NotNull(found?.Symbol);
            Assert.False(workspace.GetState().Stale);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
