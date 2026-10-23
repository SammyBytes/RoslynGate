using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RoslynGate.Mcp;

public static class McpHost
{
    public static async Task<int> RunAsync(ServerOptions options, CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(options.LogLevel);

        builder.Services.AddSingleton(options);
        builder.Services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation
                {
                    Name = "roslyngate",
                    Title = "RoslynGate",
                    Version = BuildInfo.Version
                };
                server.Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability()
                };
                server.ServerInstructions = Instructions.Text;
            })
            .WithStdioServerTransport();

        using var host = builder.Build();
        await host.RunAsync(cancellationToken);
        return 0;
    }
}
