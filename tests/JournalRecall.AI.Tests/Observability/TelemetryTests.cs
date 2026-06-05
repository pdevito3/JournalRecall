using System.Diagnostics;
using Shouldly;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Observability;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Tests.Fakes;

namespace JournalRecall.AI.Tests.Observability;

public class TelemetryTests
{
    private sealed class MaskRedactor : ITelemetryRedactor
    {
        public string Redact(string content) => "[REDACTED]";
    }

    private static IAgentRunner BuildRunner(FakeChatClient client, Action<IJournalRecallAgentsBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICapabilityResolver>(new StubCapabilityResolver([ToolFakes.Echo()]));
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, client);
        var builder = services.AddJournalRecallAgents();
        configure?.Invoke(builder);
        return services.BuildServiceProvider().GetRequiredService<IAgentRunner>();
    }

    // Unique agent name per test isolates this run's spans from any captured concurrently (the
    // ActivityListener is process-global).
    private static AgentDefinition ToolAgent(string name) => Agent.Define(name)
        .UsingModel(RunnerHarness.ModelName).WithTool("echo").WithMaxTurns(5).Build();

    private static ActivityListener Listen(List<Activity> sink)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = sink.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task Run_emits_lifecycle_spans_with_metadata()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);
        const string name = "telemetry-lifecycle";

        var client = new FakeChatClient().RequestsTool("echo", ToolFakes.TextArg("x")).RespondsWithText("ok");
        await BuildRunner(client).RunAsync(ToolAgent(name), Conversation.FromUser("hi"), new RunContext());

        var run = captured.Single(a => a.OperationName == Telemetry.Spans.Run
            && (string?)a.GetTagItem(Telemetry.Tags.AgentName) == name);
        run.GetTagItem(Telemetry.Tags.Outcome).ShouldBe("completed");

        var mine = captured.Where(a => a.TraceId == run.TraceId).Select(a => a.OperationName).ToList();
        mine.ShouldContain(Telemetry.Spans.Tool);
        mine.ShouldContain(Telemetry.Spans.ResourceAssembly);
    }

    [Fact]
    public async Task Content_is_not_captured_by_default()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);
        const string name = "telemetry-no-content";

        var client = new FakeChatClient().RequestsTool("echo", ToolFakes.TextArg("x")).RespondsWithText("ok");
        await BuildRunner(client).RunAsync(ToolAgent(name), Conversation.FromUser("hi"), new RunContext());

        var run = captured.Single(a => a.OperationName == Telemetry.Spans.Run
            && (string?)a.GetTagItem(Telemetry.Tags.AgentName) == name);
        var tool = captured.Single(a => a.OperationName == Telemetry.Spans.Tool && a.TraceId == run.TraceId);
        tool.GetTagItem(Telemetry.Tags.ToolResult).ShouldBeNull();
    }

    [Fact]
    public async Task Captured_content_passes_through_the_redaction_hook()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);
        const string name = "telemetry-redaction";

        var client = new FakeChatClient().RequestsTool("echo", ToolFakes.TextArg("secret")).RespondsWithText("ok");
        var runner = BuildRunner(client, b => b.Telemetry(t =>
        {
            t.CaptureContent = true;
            t.Redactor = new MaskRedactor();
        }));

        await runner.RunAsync(ToolAgent(name), Conversation.FromUser("hi"), new RunContext());

        var run = captured.Single(a => a.OperationName == Telemetry.Spans.Run
            && (string?)a.GetTagItem(Telemetry.Tags.AgentName) == name);
        var tool = captured.Single(a => a.OperationName == Telemetry.Spans.Tool && a.TraceId == run.TraceId);
        tool.GetTagItem(Telemetry.Tags.ToolResult).ShouldBe("[REDACTED]");
    }
}
