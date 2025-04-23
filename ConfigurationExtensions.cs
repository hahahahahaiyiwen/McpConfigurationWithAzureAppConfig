using Microsoft.Extensions.Configuration;

namespace MCPConfig
{
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Retrieves all MCP configurations from the IConfiguration instance.
        /// </summary>
        /// <param name="configuration">The IConfiguration instance to read from.</param>
        /// <param name="mcpPrefix">The prefix to look for (default is "MCP").</param>
        /// <returns>A collection of McpConfiguration objects.</returns>
        public static IEnumerable<McpConfiguration> GetMcpConfigurations(this IConfiguration configuration, string sectionName = "MCP")
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var result = new List<McpConfiguration>();
            
            // Get all configuration sections starting with the MCP prefix
            var mcpSections = configuration.GetSection(sectionName).GetChildren().ToList();
            
            foreach (var section in mcpSections)
            {
                // Get the JSON string from the configuration section
                var configJson = section.Value;
                
                if (!string.IsNullOrEmpty(section.Value))
                {
                    var mcpConfig = McpConfiguration.FromString(configJson);
                    if (mcpConfig != null)
                    {
                        result.Add(mcpConfig);
                    }
                }
            }
            
            return result;
        }
    }
}