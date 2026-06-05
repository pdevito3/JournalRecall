using JournalRecall.AI.Core.Persistence;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// The auto-registered default store (ADR-0007): a process-local, thread-safe store for
/// dev/test/single-process use. A per-thread lock makes the append primitive atomic.
/// </summary>
internal sealed class InMemoryConversationStore : StoreBase
{
    private sealed class Entry
    {
        public readonly object Gate = new();
        public readonly List<StoredMessage> Messages = [];
        public readonly HashSet<string> IdempotencyKeys = new(StringComparer.Ordinal);
        public long Version;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Entry> _threads =
        new(StringComparer.Ordinal);

    protected override Task<ThreadSnapshot?> LoadSnapshotAsync(string threadId, CancellationToken cancellationToken)
    {
        if (!_threads.TryGetValue(threadId, out var entry))
            return Task.FromResult<ThreadSnapshot?>(null);

        lock (entry.Gate)
            return Task.FromResult<ThreadSnapshot?>(new ThreadSnapshot(entry.Messages.ToArray(), entry.Version));
    }

    protected override Task<AppendOutcome> TryAppendAsync(
        string threadId,
        IReadOnlyList<StoredMessage> newMessages,
        long expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var entry = _threads.GetOrAdd(threadId, _ => new Entry());

        lock (entry.Gate)
        {
            if (idempotencyKey is not null && entry.IdempotencyKeys.Contains(idempotencyKey))
                return Task.FromResult(new AppendOutcome(AppendStatus.Duplicate, entry.Version));

            if (entry.Version != expectedVersion)
                return Task.FromResult(new AppendOutcome(AppendStatus.Conflict, entry.Version));

            entry.Messages.AddRange(newMessages);
            entry.Version++;
            if (idempotencyKey is not null)
                entry.IdempotencyKeys.Add(idempotencyKey);

            return Task.FromResult(new AppendOutcome(AppendStatus.Applied, entry.Version));
        }
    }
}
