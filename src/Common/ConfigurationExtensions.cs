using Microsoft.Extensions.Configuration;
using ModelContextProtocolClientConfiguration.Model;

namespace ModelContextProtocolClientConfiguration.Common;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Retrieves all MCP configurations from the IConfiguration instance.
    /// </summary>
    /// <param name="configuration">The IConfiguration instance to read from.</param>
    /// <param name="mcpPrefix">The prefix to look for (default is "MCP").</param>
    /// <returns>A collection of MCPClientConfiguration objects.</returns>
    public static IEnumerable<McpClientConfiguration> GetMcpClientConfigurations(this IConfiguration configuration, string sectionName = "MCP")
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var result = new List<McpClientConfiguration>();
        
        // Get all configuration sections starting with the MCP prefix
        var mcpSections = configuration.GetSection(sectionName).GetChildren().ToList();
        
        foreach (var section in mcpSections)
        {
            // Get the JSON string from the configuration section
            var configJson = section.Value;
            
            if (!string.IsNullOrEmpty(configJson))
            {
                var mcpConfig = ConfigurationConverter.FromString(configJson);

                if (mcpConfig != null)
                {
                    result.Add(mcpConfig);
                }
            }
        }
        
        return result;
    }
}