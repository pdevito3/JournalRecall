namespace JournalRecall.AI.Core.Persistence;

/// <summary>The result of an atomic append attempt by a concrete store.</summary>
public enum AppendStatus
{
    /// <summary>Messages were appended; <c>Version</c> is the new version.</summary>
    Applied,

    /// <summary>The idempotency key was already applied; <c>Version</c> is the unchanged current version.</summary>
    Duplicate,

    /// <summary>The expected version did not match; <c>Version</c> is the actual stored version.</summary>
    Conflict,
}

/// <summary>Outcome of <see cref="StoreBase.TryAppendAsync"/>.</summary>
public readonly record struct AppendOutcome(AppendStatus Status, long Version);

/// <summary>A point-in-time view of a thread for the base to wrap.</summary>
public readonly record struct ThreadSnapshot(IReadOnlyList<StoredMessage> Messages, long Version);

/// <summary>
/// Shared orchestration for the <see cref="IConversationStore"/> contract: it translates the atomic
/// append outcome into the public return value / concurrency exception, so each adapter only
/// implements the raw, atomic load/append primitives — correctness is solved once (ADR-0007).
/// </summary>
public abstract class StoreBase : IConversationStore
{
    public async Task<ConversationThread> LoadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        var snapshot = await LoadSnapshotAsync(threadId, cancellationToken);
        return new ConversationThread(threadId, snapshot?.Messages ?? [], snapshot?.Version ?? 0);
    }

    public async Task<long> AppendAsync(
        string threadId,
        IReadOnlyList<StoredMessage> newMessages,
        long expectedVersion,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentNullException.ThrowIfNull(newMessages);

        var outcome = await TryAppendAsync(threadId, newMessages, expectedVersion, idempotencyKey, cancellationToken);
        return outcome.Status switch
        {
            AppendStatus.Applied or AppendStatus.Duplicate => outcome.Version,
            AppendStatus.Conflict => throw new ConversationConcurrencyException(threadId, expectedVersion, outcome.Version),
            _ => throw new InvalidOperationException($"Unknown append status '{outcome.Status}'."),
        };
    }

    /// <summary>Loads a thread snapshot, or null if the thread does not exist.</summary>
    protected abstract Task<ThreadSnapshot?> LoadSnapshotAsync(string threadId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically appends messages, enforcing the version check and idempotency-key dedupe. Must be
    /// atomic w.r.t. concurrent appends to the same thread.
    /// </summary>
    protected abstract Task<AppendOutcome> TryAppendAsync(
        string threadId,
        IReadOnlyList<StoredMessage> newMessages,
        long expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}
