using System.Globalization;
using Microsoft.Extensions.Logging;
using RoslynGate.Mcp;

namespace RoslynGate.Cli;

internal static class OptionValues
{
    public static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    public static DetailLevel ParseDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DetailLevel.Compact;
        }

        return value.ToLowerInvariant() switch
        {
            "compact" => DetailLevel.Compact,
            "verbose" => DetailLevel.Verbose,
            _ => throw new CliException($"Invalid detail level: '{value}'. Expected compact or verbose")
        };
    }

    public static LogLevel ParseLogLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LogLevel.Error;
        }

        if (!Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level)
            || !Enum.IsDefined(level))
        {
            throw new CliException($"Invalid log level: '{value}'");
        }

        return level;
    }

    public static int ParseLimit(string name, string? value, int defaultValue, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 1)
        {
            throw new CliException($"Invalid value for {name}: '{value}'. Expected a positive integer");
        }

        return Math.Min(limit, maximum);
    }

    public static bool ParseBool(string name, string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.ToLowerInvariant() switch
        {
            "1" or "true" or "on" or "yes" => true,
            "0" or "false" or "off" or "no" => false,
            _ => throw new CliException($"Invalid value for {name}: '{value}'. Expected on or off")
        };
    }
}
