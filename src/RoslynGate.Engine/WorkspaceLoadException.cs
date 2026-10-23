namespace RoslynGate.Engine;

public sealed class WorkspaceLoadException : Exception
{
    public WorkspaceLoadException(string message) : base(message)
    {
    }

    public WorkspaceLoadException(string message, string? hint) : base(message)
    {
        Hint = hint;
    }

    public WorkspaceLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public WorkspaceLoadException(string message, string? hint, Exception innerException)
        : base(message, innerException)
    {
        Hint = hint;
    }

    public string? Hint { get; }
}
