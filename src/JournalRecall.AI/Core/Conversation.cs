using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Core;

/// <summary>
/// The input to a run: prior history (optionally tied to a thread) plus the new turn. A pure value;
/// durable loading/appending is the store's concern (ADR-0007), introduced in Phase 6.
/// </summary>
public sealed record Conversation
{
    /// <summary>Optional thread identifier when the run is part of a durable conversation.</summary>
    public string? ThreadId { get; init; }

    /// <summary>The messages the run starts from (client-supplied history + new input).</summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>A one-shot conversation from a single user message.</summary>
    public static Conversation FromUser(string text) =>
        new() { Messages = [new ChatMessage(ChatRole.User, text)] };

    /// <summary>A conversation from an explicit message list.</summary>
    public static Conversation FromMessages(params ChatMessage[] messages) =>
        new() { Messages = messages };
}
