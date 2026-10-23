namespace SampleSdk;

/// <summary>Uses widgets and gates.</summary>
public static class DoorController
{
    public static async Task RunAsync()
    {
        var widget = new Widget();
        var secure = new SecureWidget();
        IOpenable gate = new Gate();

        await widget.Open();
        await widget.Open("door-1");
        await secure.Open("door-2");
        await gate.Open("door-3");

        _ = widget.Echo(42);
        _ = widget.Serial;
    }
}
