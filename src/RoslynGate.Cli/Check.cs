using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
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
            Console.SetOut(Console.Error);

            var stopwatch = Stopwatch.StartNew();
            using var workspace = new Workspace(
                options.SolutionPath,
                NullLogger<Workspace>.Instance);
            var snapshot = await workspace.GetAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            foreach (var diagnostic in workspace.Diagnostics)
            {
                Console.Error.WriteLine($"roslyngate: warning: {diagnostic}");
            }

            output.WriteLine($"{ProductInfo.Name}  {BuildInfo.Version}");
            Write(output, CheckText.Solution, snapshot.SolutionPath);
            Write(output, CheckText.Projects, snapshot.Solution.Projects.Count().ToString());
            Write(output, CheckText.Load, $"{stopwatch.ElapsedMilliseconds}ms");
            Write(output, CheckText.Detail, options.Detail.ToString().ToLowerInvariant());
            Write(output, CheckText.RefLimit, options.ReferenceLimit.ToString());
            Write(output, CheckText.SearchLimit, options.SearchLimit.ToString());
            Write(output, CheckText.Prewarm, options.Prewarm ? CheckText.On : CheckText.Off);
            Write(output, CheckText.LogLevel, options.LogLevel.ToString().ToLowerInvariant());
            Write(output, CheckText.Status, CheckText.Ok);
            return ExitCodes.Success;
        }
        catch (SolutionNotFoundException exception)
        {
            Console.Error.WriteLine($"{CliText.ErrorPrefix}{exception.Message}");
            return ExitCodes.InvalidConfiguration;
        }
        catch (WorkspaceLoadException exception)
        {
            Console.Error.WriteLine($"{CliText.ErrorPrefix}{exception.Message}");
            return ExitCodes.InvalidConfiguration;
        }
    }

    private static void Write(TextWriter output, string label, string value)
        => output.WriteLine($"{label,-12} {value}");
}
