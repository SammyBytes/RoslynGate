namespace RoslynGate.Engine;

public sealed class DefinitionException(string code, string message, string hint) : Exception(message)
{
    public string Code { get; } = code;
    public string Hint { get; } = hint;
}
