using System.Diagnostics;
using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

public static class Check
{
    public static async Task<int> RunAsync(ServerOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var output = Console.Out;
            Console.SetOut(TextWriter.Null);

            var stopwatch = Stopwatch.StartNew();
            using var workspace = new Workspace(
                options.SolutionPath,
                message => Console.Error.WriteLine($"roslyngate: {message}"));
            var snapshot = await workspace.GetAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            output.WriteLine($"roslyngate  {BuildInfo.Version}");
            output.WriteLine($"solution    {snapshot.SolutionPath}");
            output.WriteLine($"projects    {snapshot.Solution.Projects.Count()}");
            output.WriteLine($"load        {stopwatch.ElapsedMilliseconds}ms");
            output.WriteLine($"detail      {options.Detail.ToString().ToLowerInvariant()}");
            output.WriteLine($"ref-limit   {options.ReferenceLimit}");
            output.WriteLine($"search-limit {options.SearchLimit}");
            output.WriteLine($"prewarm     {(options.Prewarm ? "on" : "off")}");
            output.WriteLine($"log-level   {options.LogLevel.ToString().ToLowerInvariant()}");
            output.WriteLine("status      ok");
            return 0;
        }
        catch (SolutionNotFoundException exception)
        {
            Console.Error.WriteLine($"roslyngate: {exception.Message}");
            return 1;
        }
        catch (WorkspaceLoadException exception)
        {
            Console.Error.WriteLine($"roslyngate: {exception.Message}");
            return 1;
        }
    }
}
