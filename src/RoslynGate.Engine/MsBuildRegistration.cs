using Microsoft.Build.Locator;

namespace RoslynGate.Engine;

internal static class MsBuildRegistration
{
    private static readonly object Gate = new();
    private static bool _registered;
    private static DotNetSdk? _sdk;
    private static string? _error;

    public static DotNetSdk? Sdk => _sdk;

    public static void Ensure(string workingDirectory)
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            var sdk = DotNetSdkLocator.Find(workingDirectory);

            if (sdk is not null)
            {
                try
                {
                    if (!MSBuildLocator.IsRegistered)
                    {
                        MSBuildLocator.RegisterMSBuildPath(sdk.Path);
                    }

                    _registered = true;
                    _sdk = sdk;
                    return;
                }
                catch (Exception exception)
                {
                    _error = exception.Message;
                }
            }

            try
            {
                if (!MSBuildLocator.IsRegistered)
                {
                    MSBuildLocator.RegisterDefaults();
                }

                _registered = true;
                _sdk = sdk;
                return;
            }
            catch (Exception exception)
            {
                var detail = _error is null ? exception.Message : $"{_error} {exception.Message}";
                throw new WorkspaceLoadException(
                    "No usable .NET SDK was found. RoslynGate needs the .NET SDK to load .NET projects.",
                    $"Install the .NET SDK from https://dot.net, or set DOTNET_ROOT to the SDK installation. Run 'dotnet --list-sdks' to verify. {detail}",
                    exception);
            }
        }
    }
}
