namespace RoslynGate.Cli;

public static class EnvironmentVariables
{
    private const string Prefix = "ROSLYGATE_";

    public const string Solution = Prefix + "SOLUTION";
    public const string LogLevel = Prefix + "LOG_LEVEL";
    public const string Detail = Prefix + "DETAIL";
    public const string RefLimit = Prefix + "REF_LIMIT";
    public const string SearchLimit = Prefix + "SEARCH_LIMIT";
    public const string Prewarm = Prefix + "PREWARM";
    public const string Watch = Prefix + "WATCH";
}
