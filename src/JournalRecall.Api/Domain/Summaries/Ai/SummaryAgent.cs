using System.Text;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;

namespace JournalRecall.Api.Domain.Summaries.Ai;

/// <summary>
/// The single-shot AI Summary agent (ADR-0004): it reads a period's worth of Session texts (the Cleaned
/// copy when present, else Raw — assembled by the caller) and returns one cohesive narrative recap of
/// the period. Expressed through the agent runner per ADR-0004 even though it needs no tools. The
/// narrative is the model's plain text — no JSON envelope to parse.
/// </summary>
public static class SummaryAgent
{
    /// <summary>Logical model name; resolved to a keyed <see cref="IChatClient"/> registered as "summary".</summary>
    public const string ModelKey = "summary";

    private const string Instructions =
        """
        You are a reflective journaling assistant. You are given the entries a user wrote across a single
        time period (a day or a week), in chronological order. Write a short, cohesive narrative summary
        of that period — a few sentences to a couple of short paragraphs.

        - Capture the throughline: what happened, recurring themes, the writer's mood and any shifts.
        - Write about the user in the third person ("they"), in a warm, plain voice.
        - Summarize and synthesize across entries; do not just restate each one in order.
        - Use only what the entries support. Do not invent events, people, or feelings.
        - Respond with ONLY the narrative prose — no preamble, headings, bullet lists, or quotes.
        """;

    /// <summary>Builds the definition. Single turn: no tools, so the first model response is final.</summary>
    public static AgentDefinition BuildDefinition() =>
        Agent.Define("period-summary")
            .UsingModel(ModelKey)
            .WithInstructions(Instructions)
            .WithMaxTurns(1)
            .Build();

    /// <summary>
    /// Assembles the user prompt for a period from its ordered Session texts: a short period header
    /// followed by each entry, separated so the model can tell them apart.
    /// </summary>
    public static string BuildPrompt(SummaryPeriod period, DateOnly anchor, IReadOnlyList<string> entries)
    {
        var (start, end) = SummaryPeriods.Range(period, anchor);
        var header = period == SummaryPeriod.Day
            ? $"Entries for {start:yyyy-MM-dd}:"
            : $"Entries for the week of {start:yyyy-MM-dd} to {end:yyyy-MM-dd}:";

        var sb = new StringBuilder().AppendLine(header).AppendLine();
        for (var i = 0; i < entries.Count; i++)
            sb.Append("Entry ").Append(i + 1).Append(": ").AppendLine(entries[i]).AppendLine();
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Extracts the narrative from the agent's terminal output (the last assistant message). Returns
    /// false when the run produced no usable text — the caller treats that as a generation failure.
    /// </summary>
    public static bool TryExtract(AgentOutcome.Completed outcome, out string content)
    {
        content = outcome.Messages
            .LastOrDefault(m => m.Role == ChatRole.Assistant)?
            .Text?
            .Trim() ?? string.Empty;
        return content.Length > 0;
    }
}
