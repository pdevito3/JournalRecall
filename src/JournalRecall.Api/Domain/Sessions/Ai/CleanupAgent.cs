using System.Text.Json;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Ai;

/// <summary>
/// The single-shot AI Cleanup agent (ADR-0004): it reads a Session's Raw text and returns a polished
/// Cleaned copy, a Synopsis, and metadata Suggestions (Topics/People/Mood) as a structured JSON object.
/// Expressed through the agent runner per ADR-0004 even though it needs no tools — the deferred chat/RAG
/// page reuses the same machinery.
/// </summary>
public static class CleanupAgent
{
    /// <summary>Logical model name; resolved to a keyed <see cref="IChatClient"/> registered as "cleanup".</summary>
    public const string ModelKey = "cleanup";

    // The known moods come straight from the MoodType SmartEnum so the prompt can never drift from the domain.
    private static readonly string KnownMoods = string.Join(", ", MoodType.Known.Select(m => m.Name));

    private static readonly string Instructions =
        $$"""
        You are a journaling cleanup assistant. You are given the Raw text a user wrote or dictated in
        a single journaling session. Produce:

        1. "cleaned": a lightly polished copy of the Raw text. Fix typos, punctuation, capitalization,
           spacing, and obvious dictation/transcription errors. Do NOT change the meaning, the voice,
           the tense, or the content. Do not add, remove, summarize, or reorder ideas. Preserve the
           user's wording wherever it is already correct. If the Raw text is empty, return an empty
           string.
        2. "synopsis": a one-to-three sentence recap of this single session, in the third person.
        3. "topics": 0-5 short life-area tags you infer (e.g. "work", "parenthood", "travel").
        4. "people": names of people referenced in the text (0-5).
        5. "mood": the writer's apparent mood as one of [{{KnownMoods}}], or null if unclear.

        These are *suggestions* the user may accept or reject; be conservative and only include what the
        text supports. Respond with ONLY a single JSON object and nothing else, in exactly this shape:
        {"cleaned": "...", "synopsis": "...", "topics": ["..."], "people": ["..."], "mood": "..." }
        """;

    /// <summary>
    /// Builds the definition. Single turn: no tools, so the first model response is final. An optional
    /// <paramref name="correctionHints"/> block (hint-mode Corrections) is appended to the instructions
    /// so the model fixes known mis-dictations in-context (issue 0009).
    /// </summary>
    public static AgentDefinition BuildDefinition(string? correctionHints = null)
    {
        var instructions = string.IsNullOrWhiteSpace(correctionHints)
            ? Instructions
            : $"{Instructions}\n\n{correctionHints}";

        return Agent.Define("session-cleanup")
            .UsingModel(ModelKey)
            .WithInstructions(instructions)
            .WithMaxTurns(1)
            .Build();
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record CleanupOutput(
        string? Cleaned, string? Synopsis, string[]? Topics, string[]? People, string? Mood);

    /// <summary>The parsed Cleanup output: the Cleaned copy, Synopsis, and proposed metadata Suggestions.</summary>
    public sealed record Parsed(
        string Cleaned,
        string Synopsis,
        IReadOnlyList<string> Topics,
        IReadOnlyList<string> People,
        Mood? Mood);

    /// <summary>
    /// Parses the agent's terminal output. Returns false when the run produced no usable JSON — the
    /// caller treats that as a Cleanup failure (Raw and any prior Cleaned copy stay intact).
    /// </summary>
    public static bool TryParse(AgentOutcome.Completed outcome, out Parsed result)
    {
        result = null!;

        var text = outcome.Messages
            .LastOrDefault(m => m.Role == ChatRole.Assistant)?
            .Text;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Be lenient about chatter or code fences around the object: take the outermost { ... }.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<CleanupOutput>(text[start..(end + 1)], Json);
            if (parsed?.Cleaned is null)
                return false;

            // A bare mood key (no Custom free text from the model); drop it if not a known mood.
            Mood? mood = null;
            if (!string.IsNullOrWhiteSpace(parsed.Mood))
                Mood.TryOf(parsed.Mood, null, out mood);

            result = new Parsed(
                parsed.Cleaned,
                parsed.Synopsis ?? string.Empty,
                parsed.Topics ?? [],
                parsed.People ?? [],
                mood);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
