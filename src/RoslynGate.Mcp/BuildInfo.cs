using System.Reflection;

namespace RoslynGate.Mcp;

public static class BuildInfo
{
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var informational = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return informational?.Split('+')[0] ?? "0.0.0";
    }
}
