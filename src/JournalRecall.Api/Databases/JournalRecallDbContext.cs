using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Databases;

/// <summary>
/// The application's single database context (file-based SQLite; ADR-0001). Hosts ASP.NET Core
/// Identity (issue 0002) and the Session aggregate (issue 0004). A global query filter scopes every
/// Session query to the current user, enforcing the Privacy invariant at the data layer.
/// </summary>
public sealed class JournalRecallDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    private readonly Guid? _currentUserId;

    public JournalRecallDbContext(DbContextOptions<JournalRecallDbContext> options, ICurrentUserService currentUser)
        : base(options) => _currentUserId = currentUser.UserId;

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Correction> Corrections => Set<Correction>();

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
            session.HasIndex(s => s.UserId);
            // Privacy invariant: referencing the instance field makes EF re-evaluate the owner per
            // query, so no User can ever read another User's Sessions (ADR-0002, CONTEXT.md).
            session.HasQueryFilter(s => s.UserId == _currentUserId);

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
        });

        modelBuilder.Entity<Correction>(correction =>
        {
            correction.ToTable("corrections");
            correction.HasKey(c => c.Id);
            correction.Ignore(c => c.DomainEvents);
            correction.HasIndex(c => c.UserId);
            // Mishearings are a primitive collection serialized to a single JSON column (EF Core).
            correction.PrimitiveCollection(c => c.Mishearings);
            // Privacy invariant: scope every Correction query to the current user (ADR-0002, CONTEXT.md).
            correction.HasQueryFilter(c => c.UserId == _currentUserId);
        });
    }
}
