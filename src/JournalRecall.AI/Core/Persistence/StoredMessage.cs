using Microsoft.Extensions.AI;

namespace JournalRecall.AI.Core.Persistence;

/// <summary>
/// A stable domain representation of a conversation message — persisted instead of raw M.E.AI types
/// for portability and redaction control (ADR-0007).
/// </summary>
public sealed record StoredMessage
{
    /// <summary>Role string ("system" | "user" | "assistant" | "tool").</summary>
    public required string Role { get; init; }

    /// <summary>The message's text content.</summary>
    public required string Text { get; init; }

    /// <summary>Optional author name.</summary>
    public string? AuthorName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The agent activity recorded while producing this message (tool calls, resource reads,
    /// delegations, progress). Populated for assistant turns produced via a streamed run; null
    /// otherwise. Not replayed into the model — it's for observability/UI only.
    /// </summary>
    public IReadOnlyList<StoredActivity>? Activity { get; init; }

    /// <summary>Cumulative token usage at the end of the turn that produced this message (assistant only).</summary>
    public long? TotalTokens { get; init; }

    /// <summary>Wall-clock duration of the run that produced this message, in milliseconds (assistant only).</summary>
    public double? DurationMs { get; init; }

    /// <summary>Maps an M.E.AI <see cref="ChatMessage"/> to its durable form.</summary>
    public static StoredMessage FromChatMessage(ChatMessage message, DateTimeOffset createdAt) => new()
    {
        Role = message.Role.Value,
        Text = message.Text,
        AuthorName = message.AuthorName,
        CreatedAt = createdAt,
    };

    /// <summary>Maps back to an M.E.AI <see cref="ChatMessage"/> for replay into the model.</summary>
    public ChatMessage ToChatMessage() => new(new ChatRole(Role), Text) { AuthorName = AuthorName };
}
