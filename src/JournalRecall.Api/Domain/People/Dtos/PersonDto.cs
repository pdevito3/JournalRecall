namespace JournalRecall.Api.Domain.People.Dtos;

/// <summary>A directory Person as the client sees it: the durable id plus its display label.</summary>
public sealed record PersonDto(Guid Id, string Label);

/// <summary>The editable fields for a directory Person (create + rename).</summary>
public sealed record PersonForWrite(string Label);
