using System.Text.Json;

namespace MCPConfig
{
    public static class McpConfigParser
    {
        public static IEnumerable<McpConfiguration> ParseMcpConfigs(string jsonConfig)
        {
            if (string.IsNullOrEmpty(jsonConfig))
            {
                return Enumerable.Empty<McpConfiguration>();
            }

            List<McpConfiguration> configurations = new();

            try
            {
                // Parse the JSON document
                using JsonDocument jsonDoc = JsonDocument.Parse(jsonConfig);
                
                // Check if "servers" property exists
                if (!jsonDoc.RootElement.TryGetProperty("servers", out JsonElement serversElement))
                {
                    return configurations;
                }

                // Process each server in the "servers" object
                foreach (JsonProperty serverProperty in serversElement.EnumerateObject())
                {
                    string serverName = serverProperty.Name;
                    JsonElement serverConfig = serverProperty.Value;
                    
                    // Skip if not an object
                    if (serverConfig.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    // Get server type
                    string serverType = serverConfig.TryGetProperty("type", out JsonElement typeElement) && 
                                        typeElement.ValueKind == JsonValueKind.String
                                        ? typeElement.GetString() ?? "unknown"
                                        : "unknown";

                    // Create a configuration object for this server
                    var config = new McpConfiguration
                    {
                        Name = serverName,
                        Type = serverType
                    };

                    // Set command and arguments for stdio type
                    if (serverType == "stdio")
                    {
                        config.Command = serverConfig.TryGetProperty("command", out JsonElement commandElement) && 
                                      commandElement.ValueKind == JsonValueKind.String
                                      ? commandElement.GetString() ?? string.Empty
                                      : string.Empty;

                        config.Arguments = serverConfig.TryGetProperty("args", out JsonElement argsElement) && 
                                     argsElement.ValueKind == JsonValueKind.Array
                                     ? argsElement.EnumerateArray()
                                       .Where(e => e.ValueKind == JsonValueKind.String)
                                       .Select(e => e.GetString())
                                       .Where(s => s != null)
                                       .Cast<string>()
                                       .ToArray()
                                     : Array.Empty<string>();
                    }
                    // Set URL for sse type
                    else if (serverType == "sse")
                    {
                        config.Url = serverConfig.TryGetProperty("url", out JsonElement urlElement) && 
                                 urlElement.ValueKind == JsonValueKind.String
                                 ? urlElement.GetString() ?? string.Empty
                                 : string.Empty;
                    }

                    configurations.Add(config);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing MCP configuration: {ex.Message}");
            }

            return configurations;
        }
    }
}