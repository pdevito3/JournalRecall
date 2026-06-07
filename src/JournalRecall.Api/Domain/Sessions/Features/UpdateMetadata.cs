using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class UpdateMetadata
{
    /// <summary>Result: Ok or NotFound (→ 404).</summary>
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

            // Manual edits are all UserSet; AI-provenance topics (if any) are preserved by the entity. Moods
            // are resolved known-vs-custom and deduped by the entity (any free text is a valid custom mood).
            // People are not edited here — they project from the prose @-mentions (RICH-007).
            session.SetUserTopics(request.Metadata.Topics ?? []);
            session.SetMoods(request.Metadata.Moods ?? []);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }
    }
}
