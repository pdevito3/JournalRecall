using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain;
using JournalRecall.Api.Domain.Admin;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Domain.People;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Domain.Summaries;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Databases;

/// <summary>
/// The application's single database context (file-based SQLite; ADR-0001). Hosts ASP.NET Core
/// Identity (issue 0002) and the Session aggregate (issue 0004). A global query filter scopes every
/// Session query to the current user, enforcing the Privacy invariant at the data layer, and every
/// <see cref="BaseEntity"/> save stamps audit fields automatically.
/// </summary>
public sealed class JournalRecallDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// The name of the per-User tenant query filter (EF Core 10 named filters). Naming it lets a query
    /// opt out of *just* tenant scoping via <c>IgnoreQueryFilters([TenantFilter])</c> without disabling
    /// any other filter — the Privacy invariant stays on by default everywhere.
    /// </summary>
    public const string TenantFilter = "Tenant";

    private readonly Guid? _currentUserId;
    private readonly TimeProvider _timeProvider;

    public JournalRecallDbContext(
        DbContextOptions<JournalRecallDbContext> options, ICurrentUserService currentUser, TimeProvider timeProvider)
        : base(options)
    {
        _currentUserId = currentUser.UserId;
        _timeProvider = timeProvider;
    }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Correction> Corrections => Set<Correction>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<AiProviderSettings> AiProviderSettings => Set<AiProviderSettings>();
    public DbSet<AuthSettings> AuthSettings => Set<AuthSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // SQLite can't order/compare DateTimeOffset — store every one as sortable UTC ticks.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Session's static mapping lives in SessionConfiguration (ADR-0012) — the first per-entity
        // IEntityTypeConfiguration<T>, picked up here. Other entities are still configured inline below;
        // migrating them into their own configs is opportunistic.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JournalRecallDbContext).Assembly);

        modelBuilder.Entity<Summary>(summary =>
        {
            summary.ToTable("summaries");
            summary.HasKey(s => s.Id);
            summary.Ignore(s => s.DomainEvents);
            // One Summary per (user, period, anchor date) — the natural key for upsert/lookup.
            summary.HasIndex(s => new { s.UserId, s.Period, s.PeriodDate }).IsUnique();
            // Tenant scoping applied automatically via ITenantScoped (ApplyTenantFilters, ADR-0012).
        });

        modelBuilder.Entity<AiProviderSettings>(provider =>
        {
            provider.ToTable("ai_provider_settings");
            provider.HasKey(p => p.Id);
            provider.Ignore(p => p.DomainEvents);
            // App-wide (not per-user): no query filter — the admin surface owns this single row.
        });

        modelBuilder.Entity<AuthSettings>(auth =>
        {
            auth.ToTable("auth_settings");
            auth.HasKey(a => a.Id);
            auth.Ignore(a => a.DomainEvents);
            // App-wide (not per-user): no query filter — the admin surface owns this single row.
        });

        modelBuilder.Entity<RefreshToken>(refreshToken =>
        {
            refreshToken.ToTable("refresh_tokens");
            refreshToken.HasKey(t => t.Id);
            // Looked up by hash on every refresh — unique so a hash collision can't shadow a token.
            refreshToken.HasIndex(t => t.TokenHash).IsUnique();
            // RevokeAll scans by user; reuse-detection revokes by chain.
            refreshToken.HasIndex(t => t.UserId);
            refreshToken.HasIndex(t => t.ChainId);
            // Deliberately NO tenant query filter: refresh runs after the access token has expired, with
            // no current user established, so a filter would hide the very row being rotated (ADR-0005).
        });

        modelBuilder.Entity<Person>(person =>
        {
            person.ToTable("people");
            person.HasKey(p => p.Id);
            person.Ignore(p => p.DomainEvents);
            // Directory lookup + autocomplete is by (user, label); also the find-or-create dedup key.
            person.HasIndex(p => new { p.UserId, p.Label });
            // Tenant scoping applied automatically via ITenantScoped (ApplyTenantFilters, ADR-0012).
        });

        modelBuilder.Entity<Correction>(correction =>
        {
            correction.ToTable("corrections");
            correction.HasKey(c => c.Id);
            correction.Ignore(c => c.DomainEvents);
            correction.HasIndex(c => c.UserId);
            // Mishearings are a primitive collection serialized to a single JSON column (EF Core),
            // read/written through the read-only property's backing field.
            correction.PrimitiveCollection(c => c.Mishearings).UsePropertyAccessMode(PropertyAccessMode.Field);
            // Tenant scoping applied automatically via ITenantScoped (ApplyTenantFilters, ADR-0012).
        });

        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>
    /// Applies the per-User <see cref="TenantFilter"/> to every <see cref="ITenantScoped"/> entity in the
    /// model (ADR-0012), replacing the hand-written per-entity filters. Reflection selects <em>which</em>
    /// entities are scoped; the predicate itself is the strongly-typed closure in
    /// <see cref="ApplyTenantFilter{TEntity}"/>, so it still references the instance field
    /// <see cref="_currentUserId"/> and EF re-evaluates it <b>per query</b>. Capturing the id's value here
    /// would bake one User into EF's cached compiled model and leak rows across tenants — the worst bug
    /// this app can have — which is exactly why the predicate is never rebuilt by reflection.
    /// </summary>
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var applyTenantFilter = typeof(JournalRecallDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
            applyTenantFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(TenantFilter, e => e.UserId == _currentUserId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stamps audit fields on every tracked <see cref="BaseEntity"/> before it is persisted: insert sets
    /// both creation and modification fields; an update sets only the modification fields. The acting User
    /// comes from the request scope (null for system/unauthenticated writes). Deletes are real — no soft
    /// delete. Owned children (Revisions, tags, suggestions) are not <see cref="BaseEntity"/> and keep
    /// their own domain timestamps — but a child-only change still touches its root (see
    /// <see cref="TouchRootsWithChangedOwnedChildren"/>).
    /// </summary>
    private void UpdateAuditFields()
    {
        TouchRootsWithChangedOwnedChildren();

        var now = _timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.UpdateCreationProperties(now, _currentUserId);
                    entry.Entity.UpdateModifiedProperties(now, _currentUserId);
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdateModifiedProperties(now, _currentUserId);
                    break;
            }
        }
    }

    /// <summary>
    /// Flips an aggregate root to Modified when only its owned children changed — a Suggestion
    /// accept/reject, a Topic/People-tag edit, a Revision append — so <c>UpdatedAt</c> advances on
    /// <b>every</b> mutation of the aggregate. EF leaves the root Unchanged in that case, which would let
    /// such writes slip past the sync change feed's <c>UpdatedAt</c> watermark (issue 0033, ADR-0013).
    /// </summary>
    private void TouchRootsWithChangedOwnedChildren()
    {
        foreach (var child in ChangeTracker.Entries()
                     .Where(e => e.Metadata.IsOwned()
                         && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            var ownership = child.Metadata.FindOwnership()!;
            // The child's FK identifies its owner; a Deleted/Modified child reads the original value
            // (an Added one has no originals — its FK can't have changed anyway).
            var ownerKey = ownership.Properties
                .Select(p => child.State == EntityState.Added ? child.CurrentValues[p] : child.OriginalValues[p])
                .ToArray();
            var owner = ChangeTracker.Entries<BaseEntity>().FirstOrDefault(o =>
                o.Metadata == ownership.PrincipalEntityType
                && ownership.PrincipalKey.Properties.Select(p => o.CurrentValues[p]).SequenceEqual(ownerKey));

            // Marking the property modified transitions an Unchanged root to Modified, so the audit
            // stamping above then advances UpdatedAt/UpdatedBy. Added/Modified roots are already stamped.
            if (owner is { State: EntityState.Unchanged })
                owner.Property(nameof(BaseEntity.UpdatedAt)).IsModified = true;
        }
    }
}
