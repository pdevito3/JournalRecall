using System.Text.Json;
using Microsoft.Extensions.AI;

namespace JournalRecall.SharedTestHelpers.Fakes.Ai;

/// <summary>
/// A deterministic stand-in for the Cleanup model (moved here from Api.Tests so any layer can drive
/// Cleanup deterministically — PRD-0003): by default it echoes the Raw text back as a "Polished: …"
/// Cleaned copy plus a Synopsis, shaped as the JSON the agent expects. The mutable switches are the
/// scripting surface; flip <see cref="Throw"/> to simulate a model failure. Sequential within a test
/// class/collection, so the switches are safe. Implements both the non-streaming and streamed
/// <see cref="IChatClient"/> paths.
/// </summary>
public sealed class ScriptableChatClient : IChatClient
{
    public bool Throw { get; set; }
    public string? CleanedOverride { get; set; }
    public string Synopsis { get; set; } = "A short recap of the session.";

    /// <summary>The system/instruction text the model last received — lets tests assert prompt injection.</summary>
    public string LastSystemText { get; private set; } = string.Empty;

    /// <summary>Metadata Suggestions the fake emits alongside the Cleaned copy (issue 0012).</summary>
    public string[] SuggestTopics { get; set; } = [];
    public string[] SuggestPeople { get; set; } = [];

    /// <summary>A single suggested mood (convenience); <see cref="SuggestMoods"/> wins when non-empty.</summary>
    public string? SuggestMood { get; set; }
    public string[] SuggestMoods { get; set; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var all = messages.ToList();
        LastSystemText = string.Join("\n", all.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        if (Throw)
            throw new InvalidOperationException("simulated model failure");

        var raw = all.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
        var cleaned = CleanedOverride ?? $"Polished: {raw}";
        var json = JsonSerializer.Serialize(new
        {
            cleanedMarkdown = cleaned,
            synopsis = Synopsis,
            topicSuggestions = SuggestTopics,
            peopleProposal = SuggestPeople,
            moodSuggestions = SuggestMoods.Length > 0
                ? SuggestMoods
                : SuggestMood is null ? Array.Empty<string>() : new[] { SuggestMood },
        });

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
        {
            Usage = new UsageDetails { TotalTokenCount = 5 },
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
            yield return new ChatResponseUpdate(message.Role, message.Contents);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
