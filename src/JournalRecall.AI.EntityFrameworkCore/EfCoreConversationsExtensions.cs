using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JournalRecall.AI.Core.Persistence;
using JournalRecall.AI.DependencyInjection;

namespace JournalRecall.AI.EntityFrameworkCore;

/// <summary>Opt-in EF Core conversation persistence (ADR-0007), the satellite to <c>JournalRecall.AI</c>.</summary>
public static class EfCoreConversationsExtensions
{
    /// <summary>
    /// Zero-config store on a dedicated <see cref="ConversationDbContext"/> (its own database). Example:
    /// <c>.WithEfCoreConversations(o => o.UseNpgsql(connectionString))</c>.
    /// </summary>
    public static IJournalRecallAgentsBuilder WithEfCoreConversations(
        this IJournalRecallAgentsBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddDbContext<ConversationDbContext>(configure);
        return builder.UseEfConversationStore<ConversationDbContext>();
    }

    /// <summary>
    /// Persists conversations into your own context — implement <see cref="IConversationDbContext"/> and
    /// call <see cref="ConversationModelExtensions.ApplyConversationModel"/> in <c>OnModelCreating</c>, then
    /// <c>.WithEfCoreConversations&lt;MyDbContext&gt;()</c>. Reuses the context's database, connection, and
    /// migrations (DataProtection's <c>PersistKeysToDbContext</c> idiom). Register the context yourself.
    /// </summary>
    public static IJournalRecallAgentsBuilder WithEfCoreConversations<TContext>(this IJournalRecallAgentsBuilder builder)
        where TContext : DbContext, IConversationDbContext
        => builder.UseEfConversationStore<TContext>();

    private static IJournalRecallAgentsBuilder UseEfConversationStore<TContext>(this IJournalRecallAgentsBuilder builder)
        where TContext : DbContext, IConversationDbContext
    {
        builder.Services.RemoveAll<IConversationStore>();
        builder.Services.AddSingleton<IConversationStore, EfConversationStore<TContext>>();
        return builder;
    }
}
