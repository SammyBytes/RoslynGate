using Microsoft.Extensions.Logging;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public sealed record ServerOptions(
    string? SolutionPath,
    DetailLevel Detail,
    int ReferenceLimit,
    int SearchLimit,
    bool Prewarm,
    LogLevel LogLevel)
{
    public const int DefaultReferenceLimit = QueryLimits.DefaultReferenceLimit;
    public const int MaxReferenceLimit = QueryLimits.MaxReferenceLimit;
    public const int DefaultSearchLimit = QueryLimits.DefaultSearchLimit;
    public const int MaxSearchLimit = QueryLimits.MaxSearchLimit;
}

public enum DetailLevel
{
    Compact,
    Verbose
}
