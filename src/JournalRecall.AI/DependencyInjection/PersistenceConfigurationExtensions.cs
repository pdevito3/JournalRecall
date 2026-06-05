using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JournalRecall.AI.Core.Persistence;

namespace JournalRecall.AI.DependencyInjection;

/// <summary>Selects the conversation persistence strategy (ADR-0007).</summary>
public static class PersistenceConfigurationExtensions
{
    /// <summary>
    /// Disables conversation persistence (one-shot / client-supplied history). Replaces the store
    /// with a no-op that never persists.
    /// </summary>
    public static IJournalRecallAgentsBuilder WithoutConversationPersistence(this IJournalRecallAgentsBuilder builder)
    {
        builder.Services.RemoveAll<IConversationStore>();
        builder.Services.AddSingleton<IConversationStore, NoOpConversationStore>();
        return builder;
    }

    /// <summary>Registers a custom <see cref="IConversationStore"/>, overriding the default.</summary>
    public static IJournalRecallAgentsBuilder WithConversationStore<TStore>(this IJournalRecallAgentsBuilder builder)
        where TStore : class, IConversationStore
    {
        builder.Services.RemoveAll<IConversationStore>();
        builder.Services.AddSingleton<IConversationStore, TStore>();
        return builder;
    }
}

/// <summary>A store that persists nothing — for one-shot or client-supplied history.</summary>
internal sealed class NoOpConversationStore : StoreBase
{
    protected override Task<ThreadSnapshot?> LoadSnapshotAsync(string threadId, CancellationToken cancellationToken) =>
        Task.FromResult<ThreadSnapshot?>(null);

    protected override Task<AppendOutcome> TryAppendAsync(
        string threadId, IReadOnlyList<StoredMessage> newMessages, long expectedVersion,
        string? idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(new AppendOutcome(AppendStatus.Applied, expectedVersion + 1));
}
