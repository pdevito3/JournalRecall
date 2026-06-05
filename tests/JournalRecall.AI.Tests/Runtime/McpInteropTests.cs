using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Runtime;

/// <summary>
/// Interop against a reference MCP server, run in-process over an in-memory pipe transport so the
/// test is deterministic and needs no external process (ADR-0003/0008).
/// </summary>
public class McpInteropTests
{
    private static Task<InMemoryMcp> StartServerAsync() => InMemoryMcp.StartAsync(
        new McpServerOptions
        {
            ServerInfo = new() { Name = "everything", Version = "1.0.0" },
            ToolCollection = [McpServerTool.Create((string message) => $"Echo: {message}", new() { Name = "echo" })],
        });

    private static ServiceProvider BuildProvider(McpClient client, FakeChatClient chat)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, chat);
        services.AddJournalRecallAgents().AddMcpServer("everything", (_, _) => Task.FromResult(client));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Mcp_tools_are_materialized_as_AIFunctions_with_authorization_descriptors()
    {
        await using var host = await StartServerAsync();
        await using var provider = BuildProvider(host.Client, new FakeChatClient());
        using var scope = provider.CreateScope();
        var resolver = provider.GetRequiredService<ICapabilityResolver>();

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithMcpServer("everything", scope: "mcp:everything").Build();

        var caps = await resolver.ResolveAsync(def, new RunContext(), scope.ServiceProvider, default);

        caps.Tools.OfType<AIFunction>().ShouldContain(t => t.Name == "echo");
        caps.ExtraToolDescriptors.ShouldContain(d => d.Name == "echo" && d.Scope == "mcp:everything");
    }

    [Fact]
    public async Task Agent_invokes_an_mcp_tool_end_to_end()
    {
        await using var host = await StartServerAsync();
        var chat = new FakeChatClient()
            .RequestsTool("echo", new Dictionary<string, object?> { ["message"] = "hi from JournalRecall" })
            .RespondsWithText("the server echoed it");
        await using var provider = BuildProvider(host.Client, chat);
        var runner = provider.GetRequiredService<IAgentRunner>();

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithMcpServer("everything").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("echo hi"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.ToolSucceeded>().ShouldContain(s => s.ToolName == "echo");
        events[^1].ShouldBeOfType<AgentEvent.Completed>();
    }

    [Fact]
    public async Task Mcp_tool_is_denied_without_server_scope()
    {
        await using var host = await StartServerAsync();
        var chat = new FakeChatClient()
            .RequestsTool("echo", new Dictionary<string, object?> { ["message"] = "x" })
            .RespondsWithText("ok");
        await using var provider = BuildProvider(host.Client, chat);
        var runner = provider.GetRequiredService<IAgentRunner>();

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName)
            .WithMcpServer("everything", scope: "mcp:everything").WithMaxTurns(5).Build();

        var events = new List<AgentEvent>();
        await foreach (var e in runner.StreamAsync(def, Conversation.FromUser("go"), new RunContext()))
            events.Add(e);

        events.OfType<AgentEvent.ToolFailed>().ShouldContain(f => f.ToolName == "echo");
        events.OfType<AgentEvent.ToolSucceeded>().ShouldBeEmpty();
    }
}
