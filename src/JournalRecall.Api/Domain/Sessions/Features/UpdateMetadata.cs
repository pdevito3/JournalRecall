using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Metadata;

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

            // Full replace (ADR-0011): every field is written wholesale — a missing list clears it, it
            // never means "leave alone". Manual edits are all UserSet; AI-provenance topics (if any) are
            // preserved by the entity. Moods are resolved known-vs-custom and deduped by the entity. Activity
            // is resolved known-vs-custom (blank → None). People are not edited here — they project from the
            // prose @-mentions (RICH-007).
            session.SetUserTopics(request.Metadata.Topics ?? []);
            session.SetMoods(request.Metadata.Moods ?? []);
            session.SetActivity(Activity.Resolve(request.Metadata.Activity));
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }
    }
}
