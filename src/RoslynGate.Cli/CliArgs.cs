using RoslynGate.Engine;
using RoslynGate.Mcp;
using static RoslynGate.Cli.OptionValues;

namespace RoslynGate.Cli;

public sealed record CliArgs(ServerOptions? Options, bool ShowHelp, bool ShowVersion, bool Check);

public static class CliArgsParser
{
    public static CliArgs Parse(string[] args)
    {
        if (args.Contains("-h") || args.Contains("--help"))
        {
            return new CliArgs(null, ShowHelp: true, ShowVersion: false, Check: false);
        }

        if (args.Contains("-V") || args.Contains("--version"))
        {
            return new CliArgs(null, ShowHelp: false, ShowVersion: true, Check: false);
        }

        var solution = NullIfEmpty(Environment.GetEnvironmentVariable("ROSLYGATE_SOLUTION"));
        var detail = ParseDetail(Environment.GetEnvironmentVariable("ROSLYGATE_DETAIL"));
        var logLevel = ParseLogLevel(Environment.GetEnvironmentVariable("ROSLYGATE_LOG_LEVEL"));
        var referenceLimit = ParseLimit(
            "ROSLYGATE_REF_LIMIT",
            Environment.GetEnvironmentVariable("ROSLYGATE_REF_LIMIT"),
            ServerOptions.DefaultReferenceLimit,
            ServerOptions.MaxReferenceLimit);
        var searchLimit = ParseLimit(
            "ROSLYGATE_SEARCH_LIMIT",
            Environment.GetEnvironmentVariable("ROSLYGATE_SEARCH_LIMIT"),
            ServerOptions.DefaultSearchLimit,
            ServerOptions.MaxSearchLimit);
        var prewarm = ParseBool(
            "ROSLYGATE_PREWARM",
            Environment.GetEnvironmentVariable("ROSLYGATE_PREWARM"),
            defaultValue: true);
        var check = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument == "--check")
            {
                check = true;
                continue;
            }

            if (argument is "-s" or "--solution")
            {
                solution = NextValue(args, ref index, argument);
                continue;
            }

            if (argument is "-l" or "--log-level")
            {
                logLevel = ParseLogLevel(NextValue(args, ref index, argument));
                continue;
            }

            if (argument == "--detail")
            {
                detail = ParseDetail(NextValue(args, ref index, argument));
                continue;
            }

            if (argument == "--ref-limit")
            {
                referenceLimit = ParseLimit(
                    "--ref-limit",
                    NextValue(args, ref index, argument),
                    ServerOptions.DefaultReferenceLimit,
                    ServerOptions.MaxReferenceLimit);
                continue;
            }

            if (argument == "--search-limit")
            {
                searchLimit = ParseLimit(
                    "--search-limit",
                    NextValue(args, ref index, argument),
                    ServerOptions.DefaultSearchLimit,
                    ServerOptions.MaxSearchLimit);
                continue;
            }

            if (argument == "--prewarm")
            {
                prewarm = true;
                continue;
            }

            if (argument == "--no-prewarm")
            {
                prewarm = false;
                continue;
            }

            if (TryReadLongValue(argument, "--solution", out var solutionValue))
            {
                solution = solutionValue;
                continue;
            }

            if (TryReadLongValue(argument, "--log-level", out var logLevelValue))
            {
                logLevel = ParseLogLevel(logLevelValue);
                continue;
            }

            if (TryReadLongValue(argument, "--detail", out var detailValue))
            {
                detail = ParseDetail(detailValue);
                continue;
            }

            if (TryReadLongValue(argument, "--ref-limit", out var referenceValue))
            {
                referenceLimit = ParseLimit(
                    "--ref-limit",
                    referenceValue,
                    ServerOptions.DefaultReferenceLimit,
                    ServerOptions.MaxReferenceLimit);
                continue;
            }

            if (TryReadLongValue(argument, "--search-limit", out var searchValue))
            {
                searchLimit = ParseLimit(
                    "--search-limit",
                    searchValue,
                    ServerOptions.DefaultSearchLimit,
                    ServerOptions.MaxSearchLimit);
                continue;
            }

            if (argument == "--")
            {
                if (index + 1 < args.Length)
                {
                    throw new CliException($"Unexpected argument: {args[index + 1]}");
                }

                break;
            }

            throw new CliException($"Unknown option: {argument}");
        }

        if (solution is not null)
        {
            solution = SolutionLocator.Locate(solution, Environment.CurrentDirectory);
        }

        var options = new ServerOptions(
            solution,
            detail,
            referenceLimit,
            searchLimit,
            prewarm,
            logLevel);

        return new CliArgs(options, ShowHelp: false, ShowVersion: false, Check: check);
    }
}
