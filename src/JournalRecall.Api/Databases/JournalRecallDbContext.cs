using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Identity;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Summaries;
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
    public DbSet<Summary> Summaries => Set<Summary>();

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
            summary.HasQueryFilter(s => s.UserId == _currentUserId);
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
