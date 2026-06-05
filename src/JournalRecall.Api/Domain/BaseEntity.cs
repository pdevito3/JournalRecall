namespace JournalRecall.Api.Domain;

/// <summary>
/// Base for rich domain aggregates: identity, automatic audit fields, and a queue of domain events
/// raised by behavior methods. Entities never expose public setters; state changes flow through
/// factories and action methods. Audit fields are stamped by the DbContext on save (see
/// <c>JournalRecallDbContext.UpdateAuditFields</c>) — the entity never sets them itself. Mirrors the
/// Wrapt/Craftsman convention.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>When the row was first persisted (UTC). Stamped automatically on insert.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The User who created the row; null for system or unauthenticated writes.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last persisted (UTC). Stamped automatically on insert and every update.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The User who last modified the row; null for system or unauthenticated writes.</summary>
    public Guid? UpdatedBy { get; private set; }

    private readonly List<DomainEvent> _domainEvents = [];
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void QueueDomainEvent(DomainEvent @event)
    {
        if (!_domainEvents.Contains(@event))
            _domainEvents.Add(@event);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Stamps the creation audit fields. Called by the persistence layer on insert only.</summary>
    public void UpdateCreationProperties(DateTimeOffset timestamp, Guid? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <summary>Stamps the modification audit fields. Called by the persistence layer on insert and update.</summary>
    public void UpdateModifiedProperties(DateTimeOffset timestamp, Guid? userId)
    {
        UpdatedAt = timestamp;
        UpdatedBy = userId;
    }
}

/// <summary>Marker base for domain events queued on an aggregate.</summary>
public abstract class DomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
