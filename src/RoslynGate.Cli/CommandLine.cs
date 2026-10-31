using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using RoslynGate.Engine;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

public static class CommandLine
{
    public static async Task<int> RunAsync(string[] args)
    {
        var defaults = CliDefaults.FromEnvironment();

        var solution = new Option<string?>(CliNames.Solution, CliNames.SolutionShort)
        {
            Description = $"Solution file or directory (env: {EnvironmentVariables.Solution}).",
            DefaultValueFactory = _ => defaults.Options.SolutionPath
        };

        var detail = new Option<DetailLevel>(CliNames.Detail)
        {
            Description = $"Output detail: compact or verbose (env: {EnvironmentVariables.Detail}).",
            DefaultValueFactory = _ => defaults.Options.Detail
        };

        var logLevel = new Option<LogLevel>(CliNames.LogLevel, CliNames.LogLevelShort)
        {
            Description = $"error, warn, info, or debug (env: {EnvironmentVariables.LogLevel}).",
            DefaultValueFactory = _ => defaults.Options.LogLevel
        };

        var referenceLimit = new Option<int>(CliNames.RefLimit)
        {
            Description = $"References per response, 1-{ServerOptions.MaxReferenceLimit} (env: {EnvironmentVariables.RefLimit}).",
            DefaultValueFactory = _ => defaults.Options.ReferenceLimit,
            CustomParser = result => ParseLimit(
                result,
                CliNames.RefLimit,
                ServerOptions.DefaultReferenceLimit,
                ServerOptions.MaxReferenceLimit)
        };

        var searchLimit = new Option<int>(CliNames.SearchLimit)
        {
            Description = $"Search hits per response, 1-{ServerOptions.MaxSearchLimit} (env: {EnvironmentVariables.SearchLimit}).",
            DefaultValueFactory = _ => defaults.Options.SearchLimit,
            CustomParser = result => ParseLimit(
                result,
                CliNames.SearchLimit,
                ServerOptions.DefaultSearchLimit,
                ServerOptions.MaxSearchLimit)
        };

        var prewarm = new Option<bool>(CliNames.Prewarm)
        {
            Description = "Load the solution in the background (default)."
        };

        var noPrewarm = new Option<bool>(CliNames.NoPrewarm)
        {
            Description = "Disable background loading."
        };

        var watch = new Option<bool>(CliNames.Watch)
        {
            Description = "Watch for .cs changes and refresh the workspace (default)."
        };

        var noWatch = new Option<bool>(CliNames.NoWatch)
        {
            Description = "Disable the file watcher."
        };

        var check = new Option<bool>(CliNames.Check)
        {
            Description = "Validate configuration and exit."
        };

        var dotnet = new Option<bool>(CliNames.DotNet)
        {
            Description = "Force the .NET server (default when a solution is present)."
        };

        var typescript = new Option<bool>(CliNames.TypeScript)
        {
            Description = "Fail and point to the roslyngate-ts package."
        };

        var root = new RootCommand($"{ProductInfo.Title} - Roslyn-powered MCP server for .NET symbols");
        root.Options.Add(solution);
        root.Options.Add(detail);
        root.Options.Add(logLevel);
        root.Options.Add(referenceLimit);
        root.Options.Add(searchLimit);
        root.Options.Add(prewarm);
        root.Options.Add(noPrewarm);
        root.Options.Add(watch);
        root.Options.Add(noWatch);
        root.Options.Add(check);
        root.Options.Add(dotnet);
        root.Options.Add(typescript);

        root.SetAction(async (parseResult, cancellationToken) =>
        {
            if (defaults.Error is not null)
            {
                Console.Error.WriteLine($"{CliText.ErrorPrefix}{defaults.Error}");
                return ExitCodes.InvalidConfiguration;
            }

            var prewarmSpecified = parseResult.GetResult(prewarm) is { Implicit: false };
            var noPrewarmSpecified = parseResult.GetResult(noPrewarm) is { Implicit: false };
            if (prewarmSpecified && noPrewarmSpecified)
            {
                Console.Error.WriteLine(
                    $"{CliText.ErrorPrefix}Use either {CliNames.Prewarm} or {CliNames.NoPrewarm}, not both.");
                return ExitCodes.InvalidConfiguration;
            }

            var prewarmValue = prewarmSpecified
                ? parseResult.GetValue(prewarm)
                : noPrewarmSpecified
                    ? !parseResult.GetValue(noPrewarm)
                    : defaults.Options.Prewarm;

            var watchSpecified = parseResult.GetResult(watch) is { Implicit: false };
            var noWatchSpecified = parseResult.GetResult(noWatch) is { Implicit: false };
            if (watchSpecified && noWatchSpecified)
            {
                Console.Error.WriteLine(
                    $"{CliText.ErrorPrefix}Use either {CliNames.Watch} or {CliNames.NoWatch}, not both.");
                return ExitCodes.InvalidConfiguration;
            }

            var watchValue = watchSpecified
                ? parseResult.GetValue(watch)
                : noWatchSpecified
                    ? !parseResult.GetValue(noWatch)
                    : defaults.Options.Watch;

            var dotnetSpecified = parseResult.GetResult(dotnet) is { Implicit: false };
            var typescriptSpecified = parseResult.GetResult(typescript) is { Implicit: false };

            if (typescriptSpecified)
            {
                return WriteWrongProject("the --ts flag");
            }

            var solutionPath = parseResult.GetValue(solution);
            if (solutionPath is not null && ProjectKind.IsTypeScriptPath(solutionPath))
            {
                return WriteWrongProject(Path.GetFileName(solutionPath));
            }

            if (solutionPath is not null)
            {
                try
                {
                    solutionPath = SolutionLocator.Locate(solutionPath, Environment.CurrentDirectory);
                }
                catch (SolutionNotFoundException exception)
                {
                    if (!dotnetSpecified
                        && TryTypeScriptProject(solutionPath, out var detail))
                    {
                        return WriteWrongProject(detail);
                    }

                    Console.Error.WriteLine($"{CliText.ErrorPrefix}{exception.Message}");
                    return ExitCodes.InvalidConfiguration;
                }
            }
            else if (!dotnetSpecified && TryTypeScriptProject(null, out var detail))
            {
                return WriteWrongProject(detail);
            }

            var options = new ServerOptions(
                solutionPath,
                parseResult.GetValue(detail),
                parseResult.GetValue(referenceLimit),
                parseResult.GetValue(searchLimit),
                prewarmValue,
                parseResult.GetValue(logLevel),
                watchValue);

            if (parseResult.GetValue(check))
            {
                return await Check.RunAsync(options, cancellationToken).ConfigureAwait(false);
            }

            return await McpHost.RunAsync(options, cancellationToken);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static bool TryTypeScriptProject(string? path, out string detail)
    {
        detail = string.Empty;
        var directory = path is null
            ? Environment.CurrentDirectory
            : Path.GetFullPath(path, Environment.CurrentDirectory);

        if (!Directory.Exists(directory) || ProjectKind.HasDotNetSolution(directory))
        {
            return false;
        }

        var config = ProjectKind.FindTypeScriptConfig(directory);
        if (config is null)
        {
            return false;
        }

        detail = Path.GetFileName(config);
        return true;
    }

    private static int WriteWrongProject(string detail)
    {
        Console.Error.WriteLine($"{CliText.ErrorPrefix}{ProjectKind.Message(detail)}");
        Console.Error.WriteLine($"{CliText.ErrorPrefix}{ProjectKind.Hint}");
        return ExitCodes.InvalidConfiguration;
    }

    private static int ParseLimit(ArgumentResult result, string name, int defaultValue, int maximum)
    {
        if (result.Tokens.Count != 1)
        {
            return defaultValue;
        }

        try
        {
            return OptionValues.ParseLimit(name, result.Tokens[0].Value, defaultValue, maximum);
        }
        catch (CliException exception)
        {
            result.AddError(exception.Message);
            return defaultValue;
        }
    }
}
