namespace JournalRecall.Api.Domain.Sessions.DomainEvents;

public sealed class SessionCreated(Guid sessionId) : DomainEvent
{
    public Guid SessionId { get; } = sessionId;
}
