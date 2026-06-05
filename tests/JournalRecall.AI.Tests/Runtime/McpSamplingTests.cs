using System.IO.Pipelines;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Runtime.Mcp;
using JournalRecall.AI.Tests.Fakes;
using Shouldly;

namespace JournalRecall.AI.Tests.Runtime;

/// <summary>
/// MCP LLM sampling: an in-process MCP server tool requests a completion from the client, which the
/// library fulfills with a configured IChatClient (ADR-0003). In-memory transport keeps it
/// deterministic (ADR-0008).
/// </summary>
public class McpSamplingTests
{
    private static Task<InMemoryMcp> StartServerAsync(IChatClient samplingModel) => InMemoryMcp.StartAsync(
        new McpServerOptions
        {
            ServerInfo = new() { Name = "sampler", Version = "1.0.0" },
            // The tool asks the *client* to run an LLM completion (sampling).
            ToolCollection =
            [
                McpServerTool.Create(
                    async (McpServer server, string text, CancellationToken ct) =>
                    {
                        var response = await server.AsSamplingChatClient(McpJsonUtilities.DefaultOptions)
                            .GetResponseAsync(text, cancellationToken: ct);
                        return response.Text;
                    },
                    new() { Name = "summarize" }),
            ],
        },
        // The client advertises sampling and routes requests to samplingModel.
        McpSampling.OptionsFor(samplingModel));

    [Fact]
    public async Task Server_tool_samples_an_llm_completion_through_the_client()
    {
        // The model that answers the server's sampling request.
        var samplingModel = new FakeChatClient().RespondsWithText("SAMPLED-SUMMARY");
        await using var host = await StartServerAsync(samplingModel);
        var client = host.Client;

        // The model that drives the agent: it calls the MCP tool, then finalizes.
        var agentModel = new FakeChatClient()
            .RequestsTool("summarize", new Dictionary<string, object?> { ["text"] = "a long article" })
            .RespondsWithText("here is the summary");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, agentModel);
        services.AddJournalRecallAgents().AddMcpServer("sampler", (_, _) => Task.FromResult(client));
        var runner = services.BuildServiceProvider().GetRequiredService<IAgentRunner>();

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithMcpServer("sampler").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("summarize it"), new RunContext()))
            events.Add(e);

        var trace = string.Join(" | ", events.Select(e => e switch
        {
            AgentEvent.ToolFailed f => $"ToolFailed({f.ToolName}:{f.Error})",
            _ => e.GetType().Name,
        }));
        events.OfType<AgentEvent.ToolSucceeded>().ShouldContain(s => s.ToolName == "summarize", trace);
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
        samplingModel.CallCount.ShouldBeGreaterThanOrEqualTo(1); // the server sampled via our client
    }
}
