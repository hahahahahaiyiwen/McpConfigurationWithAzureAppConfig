using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocolClientConfiguration.Common;
using ModelContextProtocolClientConfiguration.Model;

namespace ModelContextProtocolClientConfiguration;

public class ConfiguredMcpClient : IMcpClient
{
    private readonly IConfiguration _configuration;
    private readonly ConfiguredMcpClientOptions _options;
    private readonly SemaphoreSlim _clientsLock = new SemaphoreSlim(1, 1);
    private readonly ConcurrentDictionary<string, IMcpClient> _clients = new();
    private bool _disposed = false;
    private IMcpClient _activeClient;
    private long _nextRefreshTime;

    public ConfiguredMcpClient(IConfiguration configuration, ConfiguredMcpClientOptions options)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// The server capabilities of the active MCP client.
    /// </summary>
    public ServerCapabilities ServerCapabilities => _activeClient?.ServerCapabilities;

    /// <summary>
    /// The server information of the active MCP client.
    /// </summary>
    public Implementation ServerInfo => _activeClient?.ServerInfo;

    /// <summary>
    /// The server instructions of the active MCP client.
    /// </summary>
    public string? ServerInstructions => _activeClient?.ServerInstructions;

    public IAsyncDisposable RegisterNotificationHandler(string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConfiguredMcpClient));
        }

        return _activeClient?.RegisterNotificationHandler(method, handler);
    }

    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConfiguredMcpClient));
        }

        if (_activeClient == null)
        {
            await TryRefreshAsync();
        }

        await _activeClient.SendMessageAsync(message, cancellationToken);
    }

    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConfiguredMcpClient));
        }

        if (_activeClient == null)
        {
            await TryRefreshAsync();
        }

        return await _activeClient.SendRequestAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _clientsLock.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose all clients
            foreach (var client in _clients.Values)
            {
                await client.DisposeAsync();
            }

            _clients.Clear();

            _activeClient = null;
        }
        finally
        {
            _clientsLock.Release();

            _clientsLock.Dispose();
        }
    }

    public async Task<IList<McpClientTool>> ListToolsAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConfiguredMcpClient));
        }

        // Refresh the clients if needed
        if (Interlocked.Read(ref _nextRefreshTime) <= DateTimeOffset.UtcNow.Ticks)
        {
            await TryRefreshAsync();
        }

        await _clientsLock.WaitAsync();

        try
        {
            List<McpClientTool> consolidatedTools = new List<McpClientTool>();

            foreach (IMcpClient client in _clients.Values)
            {
                IList<McpClientTool> tools = await client.ListToolsAsync();

                consolidatedTools.AddRange(tools);
            }

            return consolidatedTools;
        }
        finally
        {
            _clientsLock.Release();
        }

    }

    private async Task TryRefreshAsync()
    {
        if (_disposed)
        {   
            throw new ObjectDisposedException(nameof(ConfiguredMcpClient));
        }


        if (Interlocked.Read(ref _nextRefreshTime) > DateTimeOffset.UtcNow.Ticks)
        {
            return;
        }

        await _clientsLock.WaitAsync();
        
        try 
        {
            IEnumerable<McpClientConfiguration>? configs = _configuration.GetMcpClientConfigurations("MCP");

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
                if (!_clients.ContainsKey(config.Name))
                {
                    IClientTransport clientTransport = CreateTransportForConfig(config);
                    IMcpClient mcpClient = await McpClientFactory.CreateAsync(clientTransport);
                    _clients[config.Name] = mcpClient;
                }
            }

            // Set the first client as active by default
            _activeClient = _clients.Values.FirstOrDefault();
            
            Interlocked.Exchange(ref _nextRefreshTime, DateTimeOffset.UtcNow.Add(_options.RefreshInterval).Ticks);
        }
        finally
        {
            _clientsLock.Release();
        }
    }

    private IClientTransport CreateTransportForConfig(McpClientConfiguration config)
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