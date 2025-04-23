using System.Text.Json;

namespace MCPConfig
{
    public class McpConfiguration
    {
        public string Name { get; set; }

        public string Command { get; set; }

        public string[] Arguments { get; set; }

        public string Type { get; set; }

        public string Url { get; set; }

        public static string ToString(McpConfiguration config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            // Create a JSON document from the configuration object
            using JsonDocument jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(config));
            
            // Convert the JSON document to a string and return it
            return jsonDoc.RootElement.ToString();
        }    
        
        public static McpConfiguration FromString(string configString)
        {
            if (string.IsNullOrEmpty(configString))
            {
                return null;
            }

            McpConfiguration configuration = new();

            try
            {
                // Parse the JSON document
                using JsonDocument jsonDoc = JsonDocument.Parse(configString);
                
                // Parse direct server configuration
                if (jsonDoc.RootElement.TryGetProperty("name", out JsonElement nameElement) && 
                    jsonDoc.RootElement.TryGetProperty("type", out JsonElement typeElement))
                {
                    // Get basic properties
                    string serverName = nameElement.GetString() ?? string.Empty;
                    string serverType = typeElement.GetString() ?? "unknown";
                    
                    configuration.Name = serverName;
                    configuration.Type = serverType;
                    
                    // Set command and arguments for stdio type
                    if (serverType == "stdio")
                    {
                        configuration.Command = jsonDoc.RootElement.TryGetProperty("command", out JsonElement commandElement) && 
                                      commandElement.ValueKind == JsonValueKind.String
                                      ? commandElement.GetString() ?? string.Empty
                                      : string.Empty;

                        configuration.Arguments = jsonDoc.RootElement.TryGetProperty("args", out JsonElement argsElement) && 
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
                        configuration.Url = jsonDoc.RootElement.TryGetProperty("url", out JsonElement urlElement) && 
                                 urlElement.ValueKind == JsonValueKind.String
                                 ? urlElement.GetString() ?? string.Empty
                                 : string.Empty;
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing MCP configuration: {ex.Message}");
            }

            return configuration;
        }
    }
}