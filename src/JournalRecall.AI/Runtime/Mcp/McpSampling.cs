using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace JournalRecall.AI.Runtime.Mcp;

/// <summary>
/// Builds <see cref="McpClientOptions"/> that let an MCP server request LLM completions from us —
/// MCP "sampling" (ADR-0003). The sampling request is fulfilled by a configured <see cref="IChatClient"/>,
/// keeping the model choice the consumer's (ADR-0002).
/// </summary>
public static class McpSampling
{
    /// <summary>Client options that advertise the sampling capability and route requests to <paramref name="samplingModel"/>.</summary>
    public static McpClientOptions OptionsFor(IChatClient samplingModel)
    {
        ArgumentNullException.ThrowIfNull(samplingModel);
        return new McpClientOptions
        {
            Capabilities = new ClientCapabilities { Sampling = new SamplingCapability() },
            Handlers = new McpClientHandlers
            {
                SamplingHandler = samplingModel.CreateSamplingHandler(McpJsonUtilities.DefaultOptions),
            },
        };
    }
}
