namespace JournalRecall.Api.Domain.People;

/// <summary>
/// A person in a User's directory (CONTEXT.md, PRD-0006): a durable, per-User entity that
/// <see cref="Sessions.Metadata.SessionPerson"/> references by id, so renaming propagates everywhere a
/// mention points. Belongs to exactly one User (Privacy invariant), enforced by the global query filter.
/// The <see cref="Label"/> is the single display name; an alias collection can be layered on later
/// without reshaping callers (aliases themselves are out of scope here).
/// </summary>
public sealed class Person : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Label { get; private set; } = string.Empty;

    private Person() { } // EF

    public static Person Create(Guid userId, string label)
    {
        var person = new Person { UserId = userId };
        person.Rename(label);
        return person;
    }

    /// <summary>Renames the directory entry in place; mentions reference the id, so the new label propagates.</summary>
    public void Rename(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label.Trim();
    }
}
