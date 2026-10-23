using Microsoft.Extensions.Logging;

namespace RoslynGate.Mcp;

public sealed record ServerOptions(
    string? SolutionPath,
    DetailLevel Detail,
    int ReferenceLimit,
    int SearchLimit,
    bool Prewarm,
    LogLevel LogLevel)
{
    public const int DefaultReferenceLimit = 50;
    public const int MaxReferenceLimit = 200;
    public const int DefaultSearchLimit = 20;
    public const int MaxSearchLimit = 50;
}

public enum DetailLevel
{
    Compact,
    Verbose
}
