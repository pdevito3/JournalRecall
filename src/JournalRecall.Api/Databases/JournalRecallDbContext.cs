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

        modelBuilder.Entity<Session>(session =>
        {
            session.ToTable("sessions");
            session.HasKey(s => s.Id);
            session.Ignore(s => s.DomainEvents);
            // Derived, not stored: Stale and the latest-Raw-Revision number are computed from the
            // Raw Revision stream + LastCleanedRawRevisionNumber (CONTEXT.md), never persisted.
            session.Ignore(s => s.EffectiveCleanupStatus);
            session.Ignore(s => s.LatestRawRevisionNumber);
            // Mood is a value object reconstructed from the scalar MoodKey/MoodCustomValue columns.
            session.Ignore(s => s.Mood);
            // Location is a value object reconstructed from the scalar Latitude/Longitude columns.
            session.Ignore(s => s.Location);
            session.HasIndex(s => s.UserId);
            // Privacy invariant: referencing the instance field makes EF re-evaluate the owner per
            // query, so no User can ever read another User's Sessions (ADR-0002, CONTEXT.md).
            session.HasQueryFilter(TenantFilter, s => s.UserId == _currentUserId);

            // The append-only Raw Revision stream is part of the Session aggregate, not an
            // independently-queried/indexed entity (ADR-0003) — hence an owned collection.
            session.OwnsMany(s => s.RawRevisions, revision =>
            {
                revision.ToTable("session_raw_revisions");
                revision.WithOwner().HasForeignKey("SessionId");
                // Store-generated shadow PK so EF reliably treats appended Revisions as inserts
                // (a client-set domain key makes EF misclassify a new child as an update).
                revision.Property<int>("Id");
                revision.HasKey("Id");
                revision.HasIndex("SessionId", nameof(RawRevision.RevisionNumber)).IsUnique();
            });

            // The Cleaned Revision stream mirrors Raw: its own append-only table, part of the Session
            // aggregate, with a store-generated shadow PK (ADR-0003; see the Raw notes above).
            session.OwnsMany(s => s.CleanedRevisions, revision =>
            {
                revision.ToTable("session_cleaned_revisions");
                revision.WithOwner().HasForeignKey("SessionId");
                revision.Property<int>("Id");
                revision.HasKey("Id");
                revision.HasIndex("SessionId", nameof(CleanedRevision.RevisionNumber)).IsUnique();
            });

            // Topic/Person tags are part of the Session aggregate (owned collections), each with a
            // store-generated shadow PK. Filtered/queried through the Session, not independently.
            session.OwnsMany(s => s.Topics, topic =>
            {
                topic.ToTable("session_topics");
                topic.WithOwner().HasForeignKey("SessionId");
                topic.Property<int>("Id");
                topic.HasKey("Id");
            });
            session.OwnsMany(s => s.People, person =>
            {
                person.ToTable("session_people");
                person.WithOwner().HasForeignKey("SessionId");
                person.Property<int>("Id");
                person.HasKey("Id");
                // One reference per (Session, Person); reverse index for a future Person filter (PRD-0006).
                person.HasIndex("SessionId", nameof(SessionPerson.PersonId)).IsUnique();
                person.HasIndex(nameof(SessionPerson.PersonId));
            });

            // Pending AI metadata Suggestions (issue 0012): an owned collection on the Session, with a
            // store-generated shadow PK. Promoted to Topics/People/Mood on accept, dropped on reject.
            session.OwnsMany(s => s.Suggestions, suggestion =>
            {
                suggestion.ToTable("session_metadata_suggestions");
                suggestion.WithOwner().HasForeignKey("SessionId");
                suggestion.Property<int>("Id");
                suggestion.HasKey("Id");
            });
        });

        modelBuilder.Entity<Summary>(summary =>
        {
            summary.ToTable("summaries");
            summary.HasKey(s => s.Id);
            summary.Ignore(s => s.DomainEvents);
            // One Summary per (user, period, anchor date) — the natural key for upsert/lookup.
            summary.HasIndex(s => new { s.UserId, s.Period, s.PeriodDate }).IsUnique();
            // Privacy invariant: scope every Summary query to the current user (ADR-0002, CONTEXT.md).
            summary.HasQueryFilter(TenantFilter, s => s.UserId == _currentUserId);
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
            // Privacy invariant: scope every Person query to the current user (ADR-0002, CONTEXT.md).
            person.HasQueryFilter(TenantFilter, p => p.UserId == _currentUserId);
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
            // Privacy invariant: scope every Correction query to the current user (ADR-0002, CONTEXT.md).
            correction.HasQueryFilter(TenantFilter, c => c.UserId == _currentUserId);
        });
    }

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
    /// their own domain timestamps.
    /// </summary>
    private void UpdateAuditFields()
    {
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
}
