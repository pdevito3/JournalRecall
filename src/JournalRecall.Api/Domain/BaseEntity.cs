namespace JournalRecall.Api.Domain;

/// <summary>
/// Base for rich domain aggregates: identity plus a queue of domain events raised by behavior
/// methods. Entities never expose public setters; state changes flow through factories and action
/// methods. Mirrors the Wrapt/Craftsman convention.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    private readonly List<DomainEvent> _domainEvents = [];
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void QueueDomainEvent(DomainEvent @event)
    {
        if (!_domainEvents.Contains(@event))
            _domainEvents.Add(@event);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Marker base for domain events queued on an aggregate.</summary>
public abstract class DomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
