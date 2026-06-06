namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// A reference from a Session to a directory <see cref="People.Person"/> by id (PRD-0006): the Person a
/// mention points at. Holds only the durable <see cref="PersonId"/> — the display label lives on the
/// Person, so renaming propagates. No provenance (it moved to the people-proposal flow, RICH-009). Part
/// of the Session aggregate (an owned collection).
/// </summary>
public sealed class SessionPerson
{
    public Guid PersonId { get; private set; }

    private SessionPerson() { } // EF

    internal SessionPerson(Guid personId) => PersonId = personId;
}
