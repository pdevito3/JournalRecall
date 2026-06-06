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

    // The known moods come straight from the Mood value object so the prompt can never drift from the domain.
    private static readonly string KnownMoods = string.Join(", ", Mood.KnownKeys);

    private static readonly string Instructions =
        $$"""
        You are a journaling cleanup assistant. You are given the Raw text a user wrote or dictated in
        a single journaling session. Produce:

        1. "cleanedMarkdown": a lightly polished copy of the Raw text, as Markdown. Fix typos, punctuation,
           capitalization, spacing, and obvious dictation/transcription errors. Do NOT change the meaning,
           the voice, the tense, or the content. Do not add, remove, summarize, or reorder ideas. Preserve
           the user's wording wherever it is already correct. Use Markdown only to reflect structure the
           text already implies (paragraphs, lists). If the Raw text is empty, return an empty string.
        2. "synopsis": a one-to-three sentence recap of this single session, in the third person.
        3. "topicSuggestions": 0-5 short life-area tags you infer (e.g. "work", "parenthood", "travel").
        4. "peopleProposal": names of people referenced in the text (0-5).
        5. "moodSuggestions": 0-3 of the writer's apparent moods, each one of [{{KnownMoods}}]; [] if unclear.

        The metadata fields are *suggestions* the user may accept or reject; be conservative and only
        include what the text supports. Respond with ONLY a single JSON object and nothing else, in
        exactly this shape:
        {"cleanedMarkdown": "...", "synopsis": "...", "topicSuggestions": ["..."], "peopleProposal": ["..."], "moodSuggestions": ["..."] }
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
        string? CleanedMarkdown, string? Synopsis,
        string[]? TopicSuggestions, string[]? PeopleProposal, string[]? MoodSuggestions);

    /// <summary>
    /// The structured Cleanup output (RICH-004): the Cleaned prose as Markdown plus the metadata
    /// side-channels. <see cref="PeopleProposal"/> is consumed by the people-proposal flow (RICH-009);
    /// <see cref="TopicSuggestions"/>/<see cref="MoodSuggestions"/> feed the metadata Suggestion chips.
    /// </summary>
    public sealed record Parsed(
        string CleanedMarkdown,
        string Synopsis,
        IReadOnlyList<string> TopicSuggestions,
        IReadOnlyList<string> PeopleProposal,
        IReadOnlyList<Mood> MoodSuggestions);

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
            if (parsed?.CleanedMarkdown is null)
                return false;

            // Resolve each proposed mood to a known-or-custom Mood; blanks are skipped.
            var moods = new List<Mood>();
            foreach (var key in parsed.MoodSuggestions ?? [])
                if (Mood.TryResolve(key, out var mood))
                    moods.Add(mood);

            result = new Parsed(
                parsed.CleanedMarkdown,
                parsed.Synopsis ?? string.Empty,
                parsed.TopicSuggestions ?? [],
                parsed.PeopleProposal ?? [],
                moods);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
