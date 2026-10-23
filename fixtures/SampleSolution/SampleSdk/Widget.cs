namespace SampleSdk;

/// <summary>A widget that can be opened.</summary>
public partial class Widget : DeviceBase, IOpenable
{
    /// <inheritdoc />
    public override string Name => "widget";

    /// <summary>Opens the widget.</summary>
    public Task Open() => Task.CompletedTask;

    /// <summary>Opens a door.</summary>
    /// <param name="doorId">Door identifier.</param>
    public Task Open(string doorId) => Task.CompletedTask;

    /// <summary>Returns the value unchanged.</summary>
    public T Echo<T>(T value) => value;
}
