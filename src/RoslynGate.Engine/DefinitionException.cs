namespace RoslynGate.Engine;

public sealed class DefinitionException(string code, string message, string hint)
    : QueryException(code, message, hint);
