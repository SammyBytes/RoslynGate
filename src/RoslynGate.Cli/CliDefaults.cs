using Microsoft.Extensions.Logging;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

internal sealed record CliDefaults(ServerOptions Options, string? Error)
{
    public static CliDefaults FromEnvironment()
    {
        var errors = new List<string>();
        var options = new ServerOptions(
            OptionValues.NullIfEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.Solution)),
            Read(errors, DetailLevel.Compact, () => OptionValues.ParseDetail(
                Environment.GetEnvironmentVariable(EnvironmentVariables.Detail))),
            Read(errors, ServerOptions.DefaultReferenceLimit, () => OptionValues.ParseLimit(
                EnvironmentVariables.RefLimit,
                Environment.GetEnvironmentVariable(EnvironmentVariables.RefLimit),
                ServerOptions.DefaultReferenceLimit,
                ServerOptions.MaxReferenceLimit)),
            Read(errors, ServerOptions.DefaultSearchLimit, () => OptionValues.ParseLimit(
                EnvironmentVariables.SearchLimit,
                Environment.GetEnvironmentVariable(EnvironmentVariables.SearchLimit),
                ServerOptions.DefaultSearchLimit,
                ServerOptions.MaxSearchLimit)),
            Read(errors, true, () => OptionValues.ParseBool(
                EnvironmentVariables.Prewarm,
                Environment.GetEnvironmentVariable(EnvironmentVariables.Prewarm),
                defaultValue: true)),
            Read(errors, LogLevel.Error, () => OptionValues.ParseLogLevel(
                Environment.GetEnvironmentVariable(EnvironmentVariables.LogLevel))),
            Read(errors, true, () => OptionValues.ParseBool(
                EnvironmentVariables.Watch,
                Environment.GetEnvironmentVariable(EnvironmentVariables.Watch),
                defaultValue: true)));

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
