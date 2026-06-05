using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JournalRecall.AI.Core;
using JournalRecall.AI.DependencyInjection;
using JournalRecall.AI.Observability;
using JournalRecall.AI.Runtime;
using JournalRecall.AI.Tests.Fakes;
using Shouldly;

namespace JournalRecall.AI.Tests.Observability;

/// <summary>
/// AI-lifecycle observability (issue 0017): content capture is off by default and, when enabled, all
/// captured content passes through the redaction hook before export; structured logs correlate to the
/// run via a correlation id; and the policy binds from configuration per environment.
/// </summary>
public class AiLifecycleObservabilityTests
{
    /// <summary>Replaces all content with a fixed marker — proves capture goes through the hook, not raw.</summary>
    private sealed class MaskRedactor : ITelemetryRedactor
    {
        public const string Mask = "[REDACTED]";
        public string Redact(string content) => Mask;
    }

    private static IAgentRunner BuildRunner(
        FakeChatClient client, IServiceCollection services, Action<IJournalRecallAgentsBuilder>? configure = null)
    {
        services.AddSingleton<ICapabilityResolver>(new StubCapabilityResolver([ToolFakes.Echo()]));
        services.AddKeyedSingleton<IChatClient>(RunnerHarness.ModelName, client);
        var builder = services.AddJournalRecallAgents();
        configure?.Invoke(builder);
        return services.BuildServiceProvider().GetRequiredService<IAgentRunner>();
    }

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
    public async Task Captured_content_passes_through_the_redactor_and_never_leaks_raw()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);
        const string name = "obs-redaction";
        const string secret = "private-journal-secret-marker";

        var client = new FakeChatClient().RequestsTool("echo", ToolFakes.TextArg(secret)).RespondsWithText("ok");
        var runner = BuildRunner(client, new ServiceCollection().AddLogging(), b => b.Telemetry(t =>
        {
            t.CaptureContent = true;
            t.Redactor = new MaskRedactor();
        }));

        await runner.RunAsync(ToolAgent(name), Conversation.FromUser("hi"), new RunContext());

        var run = captured.Single(a => a.OperationName == Telemetry.Spans.Run
            && (string?)a.GetTagItem(Telemetry.Tags.AgentName) == name);
        var tool = captured.Single(a => a.OperationName == Telemetry.Spans.Tool && a.TraceId == run.TraceId);

        // The exported span carries the redacted form — never the raw content.
        tool.GetTagItem(Telemetry.Tags.ToolResult).ShouldBe(MaskRedactor.Mask);
        foreach (var activity in captured)
            foreach (var tag in activity.Tags)
                (tag.Value ?? string.Empty).ShouldNotContain(secret);
    }

    [Fact]
    public async Task No_content_is_exported_when_capture_is_off()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);
        const string name = "obs-no-content";
        const string secret = "private-journal-secret-marker";

        // Default options: CaptureContent stays false.
        var client = new FakeChatClient().RequestsTool("echo", ToolFakes.TextArg(secret)).RespondsWithText("ok");
        var runner = BuildRunner(client, new ServiceCollection().AddLogging());

        await runner.RunAsync(ToolAgent(name), Conversation.FromUser(secret), new RunContext());

        foreach (var activity in captured)
            foreach (var tag in activity.Tags)
                (tag.Value ?? string.Empty).ShouldNotContain(secret);

        var tool = captured.Single(a => a.OperationName == Telemetry.Spans.Tool
            && a.GetTagItem(Telemetry.Tags.ToolName) is "echo");
        tool.GetTagItem(Telemetry.Tags.ToolResult).ShouldBeNull();
    }

    [Fact]
    public async Task Structured_logs_correlate_to_the_run_via_a_correlation_id()
    {
        var sink = new ScopeCapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddProvider(sink);
        });

        var client = new FakeChatClient().RequestsTool("echo", ToolFakes.TextArg("x")).RespondsWithText("ok");
        var runner = BuildRunner(client, services);
        var context = new RunContext();

        await runner.RunAsync(ToolAgent("obs-correlation"), Conversation.FromUser("hi"), context);

        // Every log emitted during the run carries the run's correlation id on its scope/state.
        sink.Entries.ShouldNotBeEmpty();
        sink.Entries.ShouldAllBe(e => e.CorrelationId == context.CorrelationId);
    }

    [Fact]
    public void Telemetry_section_binds_capture_content_per_environment_default_off()
    {
        // Absent section → default off.
        var off = new ServiceCollection();
        off.AddJournalRecallAgents().Telemetry(new ConfigurationBuilder().Build().GetSection("Telemetry"));
        off.BuildServiceProvider().GetRequiredService<IOptions<TelemetryOptions>>()
            .Value.CaptureContent.ShouldBeFalse();

        // Section enabling it → on.
        var on = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:CaptureContent"] = "true" })
            .Build();
        on.AddJournalRecallAgents().Telemetry(config.GetSection("Telemetry"));
        on.BuildServiceProvider().GetRequiredService<IOptions<TelemetryOptions>>()
            .Value.CaptureContent.ShouldBeTrue();
    }

    /// <summary>
    /// Captures the correlation id present on each log entry's active scope. Implements
    /// <see cref="ISupportExternalScope"/> so the logging factory feeds the run's scope (the same
    /// mechanism the OTLP/Serilog sinks use), and reads the correlation id from it at log time.
    /// </summary>
    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopes;
        public List<(string Message, string? CorrelationId)> Entries { get; } = [];

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
        public ILogger CreateLogger(string categoryName) => new ScopeLogger(this);
        public void Dispose() { }

        private sealed class ScopeLogger(ScopeCapturingLoggerProvider owner) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
                owner._scopes?.Push(state) ?? NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                string? correlationId = null;
                owner._scopes?.ForEachScope((scope, _) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                        foreach (var pair in pairs)
                            if (pair.Key == Telemetry.Tags.CorrelationId)
                                correlationId = pair.Value?.ToString();
                }, (object?)null);

                owner.Entries.Add((formatter(state, exception), correlationId));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
