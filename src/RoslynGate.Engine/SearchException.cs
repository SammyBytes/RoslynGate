namespace RoslynGate.Engine;

public static class SearchErrorCodes
{
    public const string QueryTooShort = "query_too_short";
}

public sealed class SearchException(string code, string message, string hint)
    : QueryException(code, message, hint);
