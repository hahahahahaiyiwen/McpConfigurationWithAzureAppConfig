using System.Text.Json;
using ModelContextProtocolClientConfiguration.Model;

namespace ModelContextProtocolClientConfiguration.Common;

public static class ConfigurationConverter
{
    public static string ToString(McpClientConfiguration config)
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

    public static McpClientConfiguration FromString(string configString)
    {
        if (string.IsNullOrEmpty(configString))
        {
            return null;
        }

        McpClientConfiguration config = new();

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
                
                config.Name = serverName;
                config.Type = serverType;
                
                // Set command and arguments for stdio type
                if (serverType == "stdio")
                {
                    config.Command = jsonDoc.RootElement.TryGetProperty("command", out JsonElement commandElement) && 
                                    commandElement.ValueKind == JsonValueKind.String
                                    ? commandElement.GetString() ?? string.Empty
                                    : string.Empty;

                    config.Arguments = jsonDoc.RootElement.TryGetProperty("args", out JsonElement argsElement) && 
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
                    config.Url = jsonDoc.RootElement.TryGetProperty("url", out JsonElement urlElement) && 
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

        return config;
    }
}