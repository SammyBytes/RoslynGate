namespace SampleSdk;

/// <summary>Something that can be opened.</summary>
public interface IOpenable
{
    /// <summary>Opens a specific door.</summary>
    Task Open(string doorId);
}
