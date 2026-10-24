namespace RoslynGate.Engine;

public sealed record WorkspaceState(bool Stale, IReadOnlyList<string> ChangedFiles)
{
    public static WorkspaceState Fresh { get; } = new(false, Array.Empty<string>());
}
