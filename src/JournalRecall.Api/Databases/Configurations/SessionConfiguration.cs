using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Content;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Databases.Configurations;

/// <summary>
/// The Session aggregate's persistence mapping (ADR-0012): the first per-entity
/// <see cref="IEntityTypeConfiguration{TEntity}"/>, auto-registered via
/// <c>ApplyConfigurationsFromAssembly</c>. Holds the static mapping only — table, key, ignores, draft
/// backing-field access, the Moods JSON collection, the Activity complex property, indexes, and the owned
/// Revision/tag/suggestion collections. The instance-dependent tenant query filter is applied separately
/// (and automatically, via the <c>ITenantScoped</c> marker) in <c>JournalRecallDbContext</c>, because it
/// must close over the live <c>_currentUserId</c> (ADR-0012).
/// </summary>
public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> session)
    {
        session.ToTable("sessions");
        session.HasKey(s => s.Id);
        session.Ignore(s => s.DomainEvents);
        // Derived, not stored: Stale and the latest-Raw-Revision number are computed from the
        // Raw Revision stream + LastCleanedRawRevisionNumber (CONTEXT.md), never persisted.
        session.Ignore(s => s.EffectiveCleanupStatus);
        session.Ignore(s => s.LatestRawRevisionNumber);
        // Location is a value object reconstructed from the scalar Latitude/Longitude columns.
        session.Ignore(s => s.Location);
        // The Draft setters derive *PlainText in lockstep; reading the backing field on materialization
        // bypasses that setter so EF doesn't re-render the stored projection on every load (ADR-0009).
        session.Property(s => s.RawDraft).UsePropertyAccessMode(PropertyAccessMode.Field);
        session.Property(s => s.CleanedDraft).UsePropertyAccessMode(PropertyAccessMode.Field);
        // Moods are a primitive string collection serialized to a single JSON column (EF Core),
        // read/written through the read-only property's backing field (PRD-0006).
        session.PrimitiveCollection(s => s.Moods).UsePropertyAccessMode(PropertyAccessMode.Field);
        // Activity is a single-valued value object whose sole persisted state is its canonical Value
        // (PRD-0007). Mapped as a complex property projecting Value to the `activity` column — no
        // ValueConverter, since Value is the only mapped scalar; known-ness is derived on read.
        session.ComplexProperty(s => s.Activity, activity =>
        {
            activity.Property(a => a.Value).HasColumnName("activity");
            // Derived on demand from Value (never stored) — kept out of the table.
            activity.Ignore(a => a.Display);
            activity.Ignore(a => a.IsKnown);
            activity.Ignore(a => a.IsNone);
            activity.Ignore(a => a.IsCustom);
        });
        // Timeline + summary source-window reads scan a user's Sessions ordered by CreatedAt; the
        // composite (UserId, CreatedAt) index serves both the tenant filter and that ordering (issue 0030).
        session.HasIndex(s => new { s.UserId, s.CreatedAt });

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
            // The GET /topics join + distinct runs off this index (PRD-0006). Denormalizing UserId
            // onto SessionTopic is deferred unless profiling demands it.
            topic.HasIndex("SessionId", nameof(SessionTopic.Name));
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
        // store-generated shadow PK. Promoted to Topics/Mood on accept, dropped on reject.
        session.OwnsMany(s => s.Suggestions, suggestion =>
        {
            suggestion.ToTable("session_metadata_suggestions");
            suggestion.WithOwner().HasForeignKey("SessionId");
            suggestion.Property<int>("Id");
            suggestion.HasKey("Id");
        });

        // Pending AI People-tag proposals (PRD-0006, RICH-009): an owned collection on the Session, with
        // a store-generated shadow PK. Resolved into inline mentions on approval, dropped on reject.
        session.OwnsMany(s => s.PeopleProposals, proposal =>
        {
            proposal.ToTable("session_people_proposals");
            proposal.WithOwner().HasForeignKey("SessionId");
            proposal.Property<int>("Id");
            proposal.HasKey("Id");
        });
    }
}
