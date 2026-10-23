using Microsoft.Extensions.Logging;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

internal sealed record CliDefaults(ServerOptions Options, string? Error)
{
    public static CliDefaults FromEnvironment()
    {
        var errors = new List<string>();
        var options = new ServerOptions(
            OptionValues.NullIfEmpty(Environment.GetEnvironmentVariable("ROSLYGATE_SOLUTION")),
            Read(errors, DetailLevel.Compact, () => OptionValues.ParseDetail(
                Environment.GetEnvironmentVariable("ROSLYGATE_DETAIL"))),
            Read(errors, ServerOptions.DefaultReferenceLimit, () => OptionValues.ParseLimit(
                "ROSLYGATE_REF_LIMIT",
                Environment.GetEnvironmentVariable("ROSLYGATE_REF_LIMIT"),
                ServerOptions.DefaultReferenceLimit,
                ServerOptions.MaxReferenceLimit)),
            Read(errors, ServerOptions.DefaultSearchLimit, () => OptionValues.ParseLimit(
                "ROSLYGATE_SEARCH_LIMIT",
                Environment.GetEnvironmentVariable("ROSLYGATE_SEARCH_LIMIT"),
                ServerOptions.DefaultSearchLimit,
                ServerOptions.MaxSearchLimit)),
            Read(errors, true, () => OptionValues.ParseBool(
                "ROSLYGATE_PREWARM",
                Environment.GetEnvironmentVariable("ROSLYGATE_PREWARM"),
                defaultValue: true)),
            Read(errors, LogLevel.Error, () => OptionValues.ParseLogLevel(
                Environment.GetEnvironmentVariable("ROSLYGATE_LOG_LEVEL"))));

        return new CliDefaults(options, errors.Count == 0 ? null : string.Join("; ", errors));
    }

    private static T Read<T>(List<string> errors, T fallback, Func<T> parse)
    {
        try
        {
            return parse();
        }
        catch (CliException exception)
        {
            errors.Add(exception.Message);
            return fallback;
        }
    }
}
