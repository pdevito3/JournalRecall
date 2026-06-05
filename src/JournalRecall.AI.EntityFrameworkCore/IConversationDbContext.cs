using Microsoft.EntityFrameworkCore;

namespace JournalRecall.AI.EntityFrameworkCore;

/// <summary>
/// Surface a DbContext must expose to back the durable conversation store (ADR-0007). Implement it on
/// your own context (and call <see cref="ConversationModelExtensions.ApplyConversationModel"/> in
/// <c>OnModelCreating</c>) to persist conversations alongside your domain in a single database — the
/// same "bring your own context" idiom as ASP.NET Data Protection's <c>PersistKeysToDbContext</c>.
/// </summary>
public interface IConversationDbContext
{
    DbSet<ConversationEntity> Conversations { get; }
    DbSet<MessageEntity> Messages { get; }
    DbSet<IdempotencyEntity> IdempotencyKeys { get; }
}

/// <summary>Configures the conversation entity model. Call from a host context's <c>OnModelCreating</c>.</summary>
public static class ConversationModelExtensions
{
    public static ModelBuilder ApplyConversationModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ConversationEntity>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.ThreadId);
            e.Property(x => x.Version).IsConcurrencyToken(); // optimistic concurrency
        });

        modelBuilder.Entity<MessageEntity>(e =>
        {
            e.ToTable("conversation_messages");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ThreadId, x.Sequence }).IsUnique();
            e.Property(x => x.Role).HasMaxLength(32);
        });

        modelBuilder.Entity<IdempotencyEntity>(e =>
        {
            e.ToTable("conversation_idempotency_keys");
            e.HasKey(x => new { x.ThreadId, x.Key });
        });

        return modelBuilder;
    }
}
