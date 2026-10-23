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

        var solution = new Option<string?>("--solution", "-s")
        {
            Description = "Solution file or directory (env: ROSLYGATE_SOLUTION).",
            DefaultValueFactory = _ => defaults.Options.SolutionPath
        };

        var detail = new Option<DetailLevel>("--detail")
        {
            Description = "Output detail: compact or verbose (env: ROSLYGATE_DETAIL).",
            DefaultValueFactory = _ => defaults.Options.Detail
        };

        var logLevel = new Option<LogLevel>("--log-level", "-l")
        {
            Description = "error, warn, info, or debug (env: ROSLYGATE_LOG_LEVEL).",
            DefaultValueFactory = _ => defaults.Options.LogLevel
        };

        var referenceLimit = new Option<int>("--ref-limit")
        {
            Description = "References per response, 1-200 (env: ROSLYGATE_REF_LIMIT).",
            DefaultValueFactory = _ => defaults.Options.ReferenceLimit,
            CustomParser = result => ParseLimit(
                result,
                "--ref-limit",
                ServerOptions.DefaultReferenceLimit,
                ServerOptions.MaxReferenceLimit)
        };

        var searchLimit = new Option<int>("--search-limit")
        {
            Description = "Search hits per response, 1-50 (env: ROSLYGATE_SEARCH_LIMIT).",
            DefaultValueFactory = _ => defaults.Options.SearchLimit,
            CustomParser = result => ParseLimit(
                result,
                "--search-limit",
                ServerOptions.DefaultSearchLimit,
                ServerOptions.MaxSearchLimit)
        };

        var prewarm = new Option<bool>("--prewarm")
        {
            Description = "Load the solution in the background (default)."
        };

        var noPrewarm = new Option<bool>("--no-prewarm")
        {
            Description = "Disable background loading."
        };

        var check = new Option<bool>("--check")
        {
            Description = "Validate configuration and exit."
        };

        var root = new RootCommand("RoslynGate - Roslyn-powered MCP server for .NET symbols");
        root.Options.Add(solution);
        root.Options.Add(detail);
        root.Options.Add(logLevel);
        root.Options.Add(referenceLimit);
        root.Options.Add(searchLimit);
        root.Options.Add(prewarm);
        root.Options.Add(noPrewarm);
        root.Options.Add(check);

        root.SetAction(async (parseResult, cancellationToken) =>
        {
            if (defaults.Error is not null)
            {
                Console.Error.WriteLine($"roslyngate: {defaults.Error}");
                return 1;
            }

            var prewarmSpecified = parseResult.GetResult(prewarm) is { Implicit: false };
            var noPrewarmSpecified = parseResult.GetResult(noPrewarm) is { Implicit: false };
            if (prewarmSpecified && noPrewarmSpecified)
            {
                Console.Error.WriteLine("roslyngate: Use either --prewarm or --no-prewarm, not both.");
                return 1;
            }

            var prewarmValue = prewarmSpecified
                ? parseResult.GetValue(prewarm)
                : noPrewarmSpecified
                    ? !parseResult.GetValue(noPrewarm)
                    : defaults.Options.Prewarm;

            var solutionPath = parseResult.GetValue(solution);
            if (solutionPath is not null)
            {
                try
                {
                    solutionPath = SolutionLocator.Locate(solutionPath, Environment.CurrentDirectory);
                }
                catch (SolutionNotFoundException exception)
                {
                    Console.Error.WriteLine($"roslyngate: {exception.Message}");
                    return 1;
                }
            }

            var options = new ServerOptions(
                solutionPath,
                parseResult.GetValue(detail),
                parseResult.GetValue(referenceLimit),
                parseResult.GetValue(searchLimit),
                prewarmValue,
                parseResult.GetValue(logLevel));

            if (parseResult.GetValue(check))
            {
                return await Check.RunAsync(options, cancellationToken).ConfigureAwait(false);
            }

            return await McpHost.RunAsync(options, cancellationToken);
        });

        return await root.Parse(args).InvokeAsync();
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
