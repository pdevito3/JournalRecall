using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Session>(session =>
        {
            session.ToTable("sessions");
            session.HasKey(s => s.Id);
            session.Ignore(s => s.DomainEvents);
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
        });
    }
}
