namespace JournalRecall.AI.EntityFrameworkCore;

/// <summary>Thread aggregate row carrying the optimistic-concurrency version.</summary>
public sealed class ConversationEntity
{
    public required string ThreadId { get; set; }
    public long Version { get; set; }
}

/// <summary>A persisted message, ordered within its thread.</summary>
public sealed class MessageEntity
{
    public Guid Id { get; set; }
    public required string ThreadId { get; set; }
    public int Sequence { get; set; }
    public required string Role { get; set; }
    public required string Text { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>This turn's agent activity, serialized as JSON (null when there was none).</summary>
    public string? ActivityJson { get; set; }

    /// <summary>Cumulative token usage at the end of the turn (assistant messages only).</summary>
    public long? TotalTokens { get; set; }

    /// <summary>Wall-clock duration of the run that produced this message, in ms (assistant messages only).</summary>
    public double? DurationMs { get; set; }
}

/// <summary>Records an applied idempotency key per thread, for safe client retries.</summary>
public sealed class IdempotencyEntity
{
    public required string ThreadId { get; set; }
    public required string Key { get; set; }
}
