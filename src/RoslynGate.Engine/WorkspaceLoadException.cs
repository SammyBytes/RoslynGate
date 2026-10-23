namespace RoslynGate.Engine;

public sealed class WorkspaceLoadException : Exception
{
    public WorkspaceLoadException(string message) : base(message)
    {
    }

    public WorkspaceLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
