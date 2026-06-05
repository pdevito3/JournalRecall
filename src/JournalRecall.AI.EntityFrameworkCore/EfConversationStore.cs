using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JournalRecall.AI.Core.Persistence;

namespace JournalRecall.AI.EntityFrameworkCore;

/// <summary>
/// EF Core-backed <see cref="IConversationStore"/> over any host context implementing
/// <see cref="IConversationDbContext"/>. A singleton that resolves a short-lived scoped context per
/// operation. Optimistic concurrency rides the <c>Version</c> concurrency token; idempotency-key
/// dedupe rides the unique key table. Orchestration is inherited from <see cref="StoreBase"/> (ADR-0007).
/// </summary>
internal sealed class EfConversationStore<TContext>(IServiceScopeFactory scopeFactory) : StoreBase
    where TContext : DbContext, IConversationDbContext
{
    private static readonly JsonSerializerOptions ActivityJsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<ThreadSnapshot?> LoadSnapshotAsync(string threadId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var conversation = await db.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ThreadId == threadId, cancellationToken);
        if (conversation is null)
            return null;

        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);

        var stored = messages
            .Select(m => new StoredMessage
            {
                Role = m.Role,
                Text = m.Text,
                AuthorName = m.AuthorName,
                CreatedAt = m.CreatedAt,
                Activity = DeserializeActivity(m.ActivityJson),
                TotalTokens = m.TotalTokens,
                DurationMs = m.DurationMs,
            })
            .ToArray();

        return new ThreadSnapshot(stored, conversation.Version);
    }

    protected override async Task<AppendOutcome> TryAppendAsync(
        string threadId,
        IReadOnlyList<StoredMessage> newMessages,
        long expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.ThreadId == threadId, cancellationToken);
        var currentVersion = conversation?.Version ?? 0;

        if (idempotencyKey is not null &&
            await db.IdempotencyKeys.AnyAsync(k => k.ThreadId == threadId && k.Key == idempotencyKey, cancellationToken))
        {
            return new AppendOutcome(AppendStatus.Duplicate, currentVersion);
        }

        if (currentVersion != expectedVersion)
            return new AppendOutcome(AppendStatus.Conflict, currentVersion);

        if (conversation is null)
        {
            conversation = new ConversationEntity { ThreadId = threadId, Version = 0 };
            db.Conversations.Add(conversation);
        }

        var sequence = await db.Messages.Where(m => m.ThreadId == threadId).CountAsync(cancellationToken);
        foreach (var message in newMessages)
        {
            db.Messages.Add(new MessageEntity
            {
                Id = Guid.CreateVersion7(),
                ThreadId = threadId,
                Sequence = sequence++,
                Role = message.Role,
                Text = message.Text,
                AuthorName = message.AuthorName,
                CreatedAt = message.CreatedAt,
                ActivityJson = SerializeActivity(message.Activity),
                TotalTokens = message.TotalTokens,
                DurationMs = message.DurationMs,
            });
        }

        conversation.Version = currentVersion + 1;

        if (idempotencyKey is not null)
            db.IdempotencyKeys.Add(new IdempotencyEntity { ThreadId = threadId, Key = idempotencyKey });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new AppendOutcome(AppendStatus.Applied, conversation.Version);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent append moved the version under us.
            return new AppendOutcome(AppendStatus.Conflict, currentVersion);
        }
        catch (DbUpdateException)
        {
            // Unique-key violation (new-thread insert race or duplicate idempotency key under contention).
            return new AppendOutcome(AppendStatus.Conflict, currentVersion);
        }
    }

    private static string? SerializeActivity(IReadOnlyList<StoredActivity>? activity) =>
        activity is { Count: > 0 } ? JsonSerializer.Serialize(activity, ActivityJsonOptions) : null;

    private static IReadOnlyList<StoredActivity>? DeserializeActivity(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<StoredActivity>>(json, ActivityJsonOptions);
}
