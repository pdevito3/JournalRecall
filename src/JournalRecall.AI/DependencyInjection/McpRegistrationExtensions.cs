using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using JournalRecall.AI.Runtime.Mcp;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>Registers external MCP servers whose tools/resources agents may consume (ADR-0003).</summary>
public static class McpRegistrationExtensions
{
    /// <summary>Registers an MCP server by logical name with a custom connection factory.</summary>
    public static IJournalRecallAgentsBuilder AddMcpServer(
        this IJournalRecallAgentsBuilder builder,
        string name,
        Func<IServiceProvider, CancellationToken, Task<McpClient>> connectAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(connectAsync);

        builder.Services.TryAddSingleton<IMcpClientProvider, McpClientProvider>();
        builder.Services.AddSingleton(new McpServerRegistration(name, connectAsync));
        return builder;
    }

    /// <summary>
    /// Registers an MCP server launched as a stdio child process (e.g. an npx reference server).
    /// When <paramref name="samplingModel"/> is set, the server may request LLM completions from us
    /// (MCP sampling), fulfilled by that keyed <see cref="IChatClient"/>.
    /// </summary>
    public static IJournalRecallAgentsBuilder AddStdioMcpServer(
        this IJournalRecallAgentsBuilder builder,
        string name,
        string command,
        IReadOnlyList<string>? arguments = null,
        string? samplingModel = null)
    {
        return builder.AddMcpServer(name, (sp, ct) =>
        {
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = name,
                Command = command,
                Arguments = arguments is null ? null : [.. arguments],
            });
            var options = samplingModel is null
                ? null
                : McpSampling.OptionsFor(sp.GetRequiredKeyedService<IChatClient>(samplingModel));
            return McpClient.CreateAsync(transport, options, cancellationToken: ct);
        });
    }
}
