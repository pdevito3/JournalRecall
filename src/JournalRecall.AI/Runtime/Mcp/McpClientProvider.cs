using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace JournalRecall.AI.Runtime.Mcp;

/// <summary>
/// Default <see cref="IMcpClientProvider"/>: caches one connected <see cref="McpClient"/> per logical
/// server name, connecting on first use under a per-name lock so concurrent runs share one client.
/// </summary>
internal sealed class McpClientProvider(
    IServiceProvider services,
    IEnumerable<McpServerRegistration> registrations) : IMcpClientProvider
{
    private readonly IReadOnlyDictionary<string, McpServerRegistration> _registrations =
        registrations.ToDictionary(r => r.Name, StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, McpClient> _clients = new(StringComparer.Ordinal);

    public async Task<McpClient> GetClientAsync(string serverName, CancellationToken cancellationToken)
    {
        if (_clients.TryGetValue(serverName, out var existing))
            return existing;

        if (!_registrations.TryGetValue(serverName, out var registration))
            throw new InvalidOperationException(
                $"No MCP server named '{serverName}' is registered. Call AddMcpServer(\"{serverName}\", ...).");

        var gate = _locks.GetOrAdd(serverName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_clients.TryGetValue(serverName, out existing))
                return existing;

            var client = await registration.ConnectAsync(services, cancellationToken);
            _clients[serverName] = client;
            return client;
        }
        finally
        {
            gate.Release();
        }
    }
}
