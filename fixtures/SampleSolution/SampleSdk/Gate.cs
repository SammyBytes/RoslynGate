namespace SampleSdk;

/// <summary>A gate that shares the openable contract with a widget.</summary>
public sealed class Gate : IOpenable
{
    public Task Open(string doorId) => Task.CompletedTask;
}
