using System.IO.Pipelines;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace JournalRecall.AI.Tests.Fakes;

/// <summary>
/// An in-memory MCP server + connected client over a pipe pair, for deterministic interop tests.
/// Disposing tears down the client AND fully stops the server's background read loop (awaiting it),
/// so no lingering loop survives into later tests and starves their async MCP delivery.
/// </summary>
internal sealed class InMemoryMcp(McpClient client, McpServer server, Task serverLoop) : IAsyncDisposable
{
    public McpClient Client { get; } = client;

    public static async Task<InMemoryMcp> StartAsync(McpServerOptions serverOptions, McpClientOptions? clientOptions = null)
    {
        Pipe clientToServer = new(), serverToClient = new();

        var server = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
            serverOptions);
        var serverLoop = server.RunAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            clientOptions);

        return new InMemoryMcp(client, server, serverLoop);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();   // completes the client->server pipe, unblocking the server loop
        await server.DisposeAsync();   // signals the server to stop
        _ = serverLoop;                // observed via disposal; not awaited (awaiting it deadlocks teardown)
    }
}
