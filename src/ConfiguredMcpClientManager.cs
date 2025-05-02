using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocolClientConfiguration.Common;
using ModelContextProtocolClientConfiguration.Model;

namespace ModelContextProtocolClientConfiguration;

public class ConfiguredMcpClientManager : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _clientsLock = new SemaphoreSlim(1, 1);
    private readonly ConcurrentDictionary<string, IMcpClient> _clients = new();
    private readonly TimeSpan RefreshAsyncTimeout = TimeSpan.FromSeconds(20);
    public readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private long _nextRefreshTicks = 0;
    private bool _disposed = false;

    public ConfiguredMcpClientManager(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Use interlocked to ensure thread safety for the disposed flag
        if (Interlocked.Exchange(ref _disposed, true) != false)
        {
            return;
        }

        await _clientsLock.WaitAsync();

        try
        {
            // Dispose all clients
            foreach (var client in _clients.Values)
            {
                await client.DisposeAsync();
            }

            _clients.Clear();
        }
        finally
        {
            _clientsLock.Release();

            _clientsLock.Dispose();
        }
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConfiguredMcpClientManager));
        }

        await TryRefreshAsync(cancellationToken);

        List<McpClientTool> consolidatedTools = new List<McpClientTool>();

        foreach (IMcpClient client in _clients.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

            consolidatedTools.AddRange(tools);
        }

        return consolidatedTools;
    }

    private async Task TryRefreshAsync(CancellationToken cancellationToken)
    {
        long nowTicks = DateTimeOffset.UtcNow.Ticks;

        if (Interlocked.Read(ref _nextRefreshTicks) <= nowTicks &&
            Interlocked.Exchange(ref _nextRefreshTicks, nowTicks + RefreshInterval.Ticks) <= nowTicks)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            cts.CancelAfter(RefreshAsyncTimeout);

            _ = RefreshClientsAsync(cts.Token).ContinueWith(
                t =>
                {
                    cts.Dispose();
                });
        }
    }

    private async Task RefreshClientsAsync(CancellationToken cancellationToken)
    {
        await _clientsLock.WaitAsync(cancellationToken);

        try
        {
            IEnumerable<McpClientConfiguration> configs = _configuration.GetMcpClientConfigurations("MCP");

            if (configs == null || !configs.Any())
            {
                _clients.Clear();

                return;
            }

            // Create a set of current config names for quick lookup
            HashSet<string> currentConfigNames = new HashSet<string>(
                configs.Select(c => c.Name), 
                StringComparer.OrdinalIgnoreCase);
            
            // Find and remove clients that are no longer in the configuration
            List<string> clientsToRemove = _clients.Keys
                .Where(clientName => !currentConfigNames.Contains(clientName))
                .ToList();

            foreach (var clientName in clientsToRemove)
            {
                _ = _clients.TryRemove(clientName, out _);
            }

            // Initialize clients for each configuration
            foreach (McpClientConfiguration config in configs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IMcpClient mcpClient = await McpClientFactory.CreateAsync(
                    CreateClientTransport(config),
                    cancellationToken: cancellationToken);

                if (!_clients.TryAdd(config.Name, mcpClient))
                {
                    await mcpClient.DisposeAsync();
                }
            }
        }
        finally
        {
            _clientsLock.Release();
        }
    }

    private IClientTransport CreateClientTransport(McpClientConfiguration config)
    {
        return config.Type.ToLower() switch
        {
            "sse" => new SseClientTransport(
                new SseClientTransportOptions()
                {
                    Endpoint = new Uri(config.Url),
                    Name = config.Name
                }),

            "stdio" => new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Command = config.Command,
                    Arguments = config.Arguments,
                    Name = config.Name
                }),

            _ => throw new NotImplementedException($"Unsupported server type: {config.Type}")
        };
    }
}