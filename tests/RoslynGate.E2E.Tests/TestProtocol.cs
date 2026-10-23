using RoslynGate.Mcp;

namespace RoslynGate.E2E.Tests;

internal static class TestProtocol
{
    public const string JsonRpcVersion = "2.0";
    public const string ServerName = ProductInfo.Name;
    public static string Version => BuildInfo.Version;

    public static readonly TimeSpan PrewarmWait = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(60);
}
