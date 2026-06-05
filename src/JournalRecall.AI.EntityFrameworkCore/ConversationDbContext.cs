using Microsoft.EntityFrameworkCore;

namespace JournalRecall.AI.EntityFrameworkCore;

/// <summary>
/// Standalone EF Core context backing the durable conversation store (ADR-0007), used by the
/// zero-config <c>WithEfCoreConversations(options)</c> overload. Consumers that prefer a single
/// database can instead implement <see cref="IConversationDbContext"/> on their own context.
/// </summary>
public sealed class ConversationDbContext(DbContextOptions<ConversationDbContext> options)
    : DbContext(options), IConversationDbContext
{
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<IdempotencyEntity> IdempotencyKeys => Set<IdempotencyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConversationModel();
}
