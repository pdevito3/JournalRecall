using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class UpdateMetadata
{
    /// <summary>Result: Ok or NotFound (→ 404). An invalid Mood throws and is mapped to 422 by the
    /// ProblemDetails pipeline.</summary>
    public enum Result { Ok, NotFound }

    public sealed record Command(Guid SessionId, MetadataForWrite Metadata) : IRequest<Result>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return Result.NotFound;

            // An invalid Mood throws (InvalidSmartEnumPropertyName / ValidationException) and propagates
            // to the ProblemDetails middleware as a 422.
            var mood = request.Metadata.Mood is { } m ? Mood.Of(m.Key, m.CustomValue) : null;

            // Manual edits are all UserSet; AI-provenance tags (if any) are preserved by the entity.
            session.SetUserTopics(request.Metadata.Topics ?? []);
            session.SetUserPeople(request.Metadata.People ?? []);
            session.SetMood(mood);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }
    }
}
