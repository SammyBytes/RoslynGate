namespace RoslynGate.Engine;

public abstract class QueryException(string code, string message, string hint) : Exception(message)
{
    public string Code { get; } = code;

    public string Hint { get; } = hint;
}
