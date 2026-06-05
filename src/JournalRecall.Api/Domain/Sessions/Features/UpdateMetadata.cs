using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class UpdateMetadata
{
    /// <summary>Result: Ok, NotFound (→ 404), or InvalidMood (→ 400).</summary>
    public enum Result { Ok, NotFound, InvalidMood }

    public sealed record Command(Guid SessionId, MetadataForWrite Metadata) : IRequest<Result>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return Result.NotFound;

            Mood? mood;
            try
            {
                mood = request.Metadata.Mood is { } m ? Mood.Of(m.Key, m.CustomValue) : null;
            }
            catch (ArgumentException)
            {
                return Result.InvalidMood;
            }

            // Manual edits are all UserSet; AI-provenance tags (if any) are preserved by the entity.
            session.SetUserTopics(request.Metadata.Topics ?? []);
            session.SetUserPeople(request.Metadata.People ?? []);
            session.SetMood(mood);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }
    }
}
