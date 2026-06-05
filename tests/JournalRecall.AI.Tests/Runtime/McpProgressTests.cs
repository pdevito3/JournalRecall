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
using JournalRecall.AI.Tests.Fakes;
using Shouldly;

namespace JournalRecall.AI.Tests.Runtime;

/// <summary>
/// MCP progress notifications: a long-running server tool reports progress, which the library forwards
/// onto the agent event stream as <see cref="AgentEvent.Progress"/> (ADR-0005). Asserted at the
/// resolver layer (the wrapped tool + progress queue the runner drains) so the test is deterministic —
/// MCP progress is delivered asynchronously, so a bounded wait stands in for fake-model run timing.
/// </summary>
public class McpProgressTests
{
    private static Task<InMemoryMcp> StartServerAsync() => InMemoryMcp.StartAsync(
        new McpServerOptions
        {
            ServerInfo = new() { Name = "worker", Version = "1.0.0" },
            ToolCollection =
            [
                McpServerTool.Create(
                    (IProgress<ProgressNotificationValue> progress) =>
                    {
                        progress.Report(new ProgressNotificationValue { Progress = 50, Total = 100, Message = "halfway" });
                        progress.Report(new ProgressNotificationValue { Progress = 100, Total = 100, Message = "done" });
                        return "task complete";
                    },
                    new() { Name = "longtask" }),
            ],
        });

    [Fact]
    public async Task Mcp_tool_progress_is_forwarded_onto_the_progress_queue()
    {
        await using var host = await StartServerAsync();
        var client = host.Client;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, new FakeChatClient());
        services.AddJournalRecallAgents().AddMcpServer("worker", (_, _) => Task.FromResult(client));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var def = Agent.Define("a").UsingModel(RunnerHarness.ModelName).WithMcpServer("worker").Build();
        var caps = await provider.GetRequiredService<ICapabilityResolver>()
            .ResolveAsync(def, new RunContext(), scope.ServiceProvider, default);

        // The resolver wraps MCP tools so their progress notifications enqueue AgentEvent.Progress.
        // MCP progress is delivered asynchronously over the session; re-invoke and wait until it lands
        // (resilient to transient scheduling delays when many in-memory sessions share the process).
        var longtask = caps.Tools.OfType<AIFunction>().Single(t => t.Name == "longtask");
        for (var attempt = 0; attempt < 5 && caps.ProgressEvents.Count < 2; attempt++)
        {
            await longtask.InvokeAsync(new AIFunctionArguments());
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (caps.ProgressEvents.Count < 2 && DateTime.UtcNow < deadline)
                await Task.Delay(20);
        }

        var progress = caps.ProgressEvents.OfType<AgentEvent.Progress>().ToList();
        progress.ShouldContain(p => p.Message == "halfway" && p.Value == 50 && p.Total == 100);
        progress.ShouldContain(p => p.Message == "done" && p.Value == 100);
    }
}
