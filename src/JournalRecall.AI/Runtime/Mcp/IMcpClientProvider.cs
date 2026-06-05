using ModelContextProtocol.Client;

namespace JournalRecall.AI.Runtime.Mcp;

/// <summary>
/// Pools live <see cref="McpClient"/>s as DI singletons, created lazily from registered factories and
/// reused across runs (ADR-0003). The connection (I/O) happens once, on first use.
/// </summary>
public interface IMcpClientProvider
{
    Task<McpClient> GetClientAsync(string serverName, CancellationToken cancellationToken);
}

/// <summary>A registered MCP server: a logical name and a factory that connects a client.</summary>
public sealed record McpServerRegistration(
    string Name,
    Func<IServiceProvider, CancellationToken, Task<McpClient>> ConnectAsync);
