namespace JournalRecall.AI.Core.Persistence;

/// <summary>A loaded thread: its messages and the version used for optimistic concurrency.</summary>
public sealed record ConversationThread(string ThreadId, IReadOnlyList<StoredMessage> Messages, long Version);

/// <summary>
/// The conversation persistence port (ADR-0007). The library ships an in-memory default; durable
/// stores are opt-in. Correctness (optimistic concurrency + idempotency-key dedupe) lives in the
/// contract and is implemented once by <see cref="StoreBase"/>.
/// </summary>
public interface IConversationStore
{
    /// <summary>Loads a thread's messages and current version (version 0 for a new/empty thread).</summary>
    Task<ConversationThread> LoadAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends messages with optimistic concurrency (<paramref name="expectedVersion"/>) and optional
    /// idempotency-key dedupe. Returns the new version. Throws
    /// <see cref="ConversationConcurrencyException"/> on a version clash; a duplicate idempotency key
    /// is a no-op that returns the current version.
    /// </summary>
    Task<long> AppendAsync(
        string threadId,
        IReadOnlyList<StoredMessage> newMessages,
        long expectedVersion,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Thrown when an append's expected version does not match the stored version.</summary>
public sealed class ConversationConcurrencyException(string threadId, long expectedVersion, long actualVersion)
    : Exception($"Concurrency clash on thread '{threadId}': expected version {expectedVersion}, found {actualVersion}.")
{
    public string ThreadId { get; } = threadId;
    public long ExpectedVersion { get; } = expectedVersion;
    public long ActualVersion { get; } = actualVersion;
}
