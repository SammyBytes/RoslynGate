using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynGate.Engine;

namespace RoslynGate.Mcp;

public static class McpHost
{
    public static async Task<int> RunAsync(ServerOptions options, CancellationToken cancellationToken = default)
    {
        // stdout belongs to the MCP transport. Accidental Console.Out writes go to stderr.
        Console.SetOut(Console.Error);

        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(options.LogLevel);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(serviceProvider => new Workspace(
            options.SolutionPath,
            serviceProvider.GetRequiredService<ILogger<Workspace>>()));

        builder.Services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation
                {
                    Name = ProductInfo.Name,
                    Title = ProductInfo.Title,
                    Version = BuildInfo.Version
                };
                server.Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability()
                };
                server.ServerInstructions = Instructions.Text;
            })
            .WithStdioServerTransport()
            .WithTools<DefinitionTool>();

        using var host = builder.Build();

        if (options.Prewarm)
        {
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            host.Services.GetRequiredService<Workspace>().StartPrewarm(lifetime.ApplicationStopping);
        }

        await host.RunAsync(cancellationToken);
        return 0;
    }
}
