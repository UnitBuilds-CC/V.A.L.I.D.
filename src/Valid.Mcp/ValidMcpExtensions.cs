using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Valid.Mcp;

/// <summary>
/// Extension methods to register the VALID MCP Server in a host application.
/// </summary>
public static class ValidMcpExtensions
{
    /// <summary>
    /// Adds the VALID MCP Server to the service collection.
    /// This enables AI agents to connect via stdio or HTTP and interact with ValidObjects.
    /// </summary>
    public static IServiceCollection AddValidMcpServer(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<ValidTools>();

        return services;
    }
}

