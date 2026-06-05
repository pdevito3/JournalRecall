using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JournalRecall.AI.Observability;

/// <summary>
/// The library's <see cref="ActivitySource"/>, <see cref="Meter"/>, and span/tag/instrument names.
/// Consumers add this source (traces) and meter (metrics) to their OpenTelemetry providers to receive
/// spans and metrics for the outer loop, turns, tool calls, resource assembly, and delegation
/// (ADR-0005). M.E.AI's own <c>UseOpenTelemetry</c> contributes the GenAI model spans/metrics.
/// </summary>
public static class Telemetry
{
    public const string SourceName = "JournalRecall.AI";
    public const string MeterName = "JournalRecall.AI";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    /// <summary>Metric instruments for the agent lifecycle (metadata only; no content — ADR-0005).</summary>
    public static class Metrics
    {
        public static readonly Counter<long> Runs =
            Meter.CreateCounter<long>("journalrecall.agent.runs", "{run}", "Completed agent runs, tagged by outcome.");

        public static readonly Counter<long> Turns =
            Meter.CreateCounter<long>("journalrecall.agent.turns", "{turn}", "Model turns executed.");

        public static readonly Counter<long> ToolCalls =
            Meter.CreateCounter<long>("journalrecall.agent.tool_calls", "{call}", "Tool invocations, tagged by status.");

        public static readonly Counter<long> Tokens =
            Meter.CreateCounter<long>("journalrecall.agent.tokens", "{token}", "Total tokens consumed.");

        public static readonly Histogram<double> RunDuration =
            Meter.CreateHistogram<double>("journalrecall.agent.run.duration", "ms", "Wall-clock run duration.");
    }

    public static class Spans
    {
        public const string Run = "journalrecall.agent.run";
        public const string Turn = "journalrecall.agent.turn";
        public const string ResourceAssembly = "journalrecall.resource.assembly";
        public const string Tool = "journalrecall.tool.invoke";
        public const string Delegate = "journalrecall.agent.delegate";
    }

    public static class Tags
    {
        public const string AgentName = "journalrecall.agent.name";
        public const string CorrelationId = "journalrecall.correlation_id";
        public const string Model = "gen_ai.request.model";
        public const string Turn = "journalrecall.turn";
        public const string Outcome = "journalrecall.outcome";
        public const string StopReason = "journalrecall.stop_reason";
        public const string TotalTokens = "gen_ai.usage.total_tokens";
        public const string ToolName = "gen_ai.tool.name";
        public const string ErrorType = "error.type";
        public const string DelegateAgent = "journalrecall.delegate.agent";
        public const string ToolResult = "journalrecall.tool.result"; // content; only when capture is on
    }
}
