using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

public static class Check
{
    public static async Task<int> RunAsync(ServerOptions options, CancellationToken cancellationToken)
    {
        var output = Console.Out;

        try
        {
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
            Write(output, CheckText.Sdk, Workspace.Sdk?.Version ?? "MSBuild default");
            if (Workspace.Sdk is not null)
            {
                Write(output, CheckText.SdkPath, Workspace.Sdk.Path);
            }

            Write(output, CheckText.Projects, snapshot.Solution.Projects.Count().ToString());
            Write(output, CheckText.Load, $"{stopwatch.ElapsedMilliseconds}ms");
            Write(output, CheckText.Detail, options.Detail.ToString().ToLowerInvariant());
            Write(output, CheckText.RefLimit, options.ReferenceLimit.ToString());
            Write(output, CheckText.SearchLimit, options.SearchLimit.ToString());
            Write(output, CheckText.Prewarm, options.Prewarm ? CheckText.On : CheckText.Off);
            Write(output, CheckText.Watch, options.Watch ? CheckText.On : CheckText.Off);
            Write(output, CheckText.LogLevel, options.LogLevel.ToString().ToLowerInvariant());
            Write(output, CheckText.Status, CheckText.Ok);
            return ExitCodes.Success;
        }
        catch (SolutionNotFoundException exception)
        {
            WriteFailure(output, exception.Message, null);
            return ExitCodes.InvalidConfiguration;
        }
        catch (WorkspaceLoadException exception)
        {
            WriteFailure(output, exception.Message, exception.Hint);
            return ExitCodes.InvalidConfiguration;
        }
    }

    private static void WriteFailure(TextWriter output, string message, string? hint)
    {
        Console.Error.WriteLine($"{CliText.ErrorPrefix}{message}");
        if (!string.IsNullOrWhiteSpace(hint))
        {
            Console.Error.WriteLine($"{CliText.ErrorPrefix}hint: {hint}");
        }

        Write(output, CheckText.Status, CheckText.Failed);
    }

    private static void Write(TextWriter output, string label, string value)
        => output.WriteLine($"{label,-12} {value}");
}
