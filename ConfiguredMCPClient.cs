using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace MCPConfig
{
    public class ConfiguredMCPClient
    {
        private readonly IConfiguration _config;

        public ConfiguredMCPClient(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<IList<McpClientTool>> ListToolsAsync()
        {
            List<McpClientTool> consolidatedTools = new List<McpClientTool>();

            IEnumerable<McpConfiguration>? configs = _config.GetMcpConfigurations("MCP");

            if (configs == null || !configs.Any())
            {
                return consolidatedTools;
            }

            IClientTransport clientTransport;

            foreach (McpConfiguration config in configs)
            {
                switch (config.Type.ToLower())
                {
                    case "sse":
                        clientTransport = new SseClientTransport(
                            new SseClientTransportOptions()
                            {
                                Endpoint = new Uri(config.Url),
                                Name = config.Name
                            });
                        break;

                    case "stdio":
                        clientTransport = new StdioClientTransport(
                            new StdioClientTransportOptions
                            {
                                Command = config.Command,
                                Arguments = config.Arguments,
                                Name = config.Name
                            });
                        break;

                    default:
                        throw new NotImplementedException($"Unsupported server type: {config.Type}");
                }

                IMcpClient mcpClient = await McpClientFactory.CreateAsync(clientTransport);
                    
                IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

                Console.WriteLine($"System: fetching tools from {config.Name}- {string.Join(", ", tools.Select(t => t.Name).Take(5))}...");
                
                consolidatedTools.AddRange(tools);
            }

            return consolidatedTools;
        }
    }
}