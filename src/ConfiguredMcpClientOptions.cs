namespace ModelContextProtocolClientConfiguration;

public class ConfiguredMcpClientOptions
{
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
}