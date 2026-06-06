using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.People;
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

            // People arrive as labels; resolve them to directory Person ids (creating any new ones), so
            // SessionPerson references the durable entity. RICH-007 replaces this text path with @-mentions.
            var personIds = await ResolvePeopleAsync(session.UserId, request.Metadata.People ?? [], cancellationToken);

            // Manual edits are all UserSet; AI-provenance topics (if any) are preserved by the entity. Moods
            // are resolved known-vs-custom and deduped by the entity (any free text is a valid custom mood).
            session.SetUserTopics(request.Metadata.Topics ?? []);
            session.SetUserPeople(personIds);
            session.SetMoods(request.Metadata.Moods ?? []);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }

        /// <summary>Find-or-create each label in the User's directory (case-insensitive), returning their ids.</summary>
        private async Task<IReadOnlyList<Guid>> ResolvePeopleAsync(
            Guid userId, IReadOnlyList<string> labels, CancellationToken cancellationToken)
        {
            var normalized = labels
                .Select(l => l?.Trim() ?? string.Empty)
                .Where(l => l.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalized.Count == 0)
                return [];

            // The directory is small (per-user); match case-insensitively in memory to mirror dedup rules.
            var directory = await db.People.ToListAsync(cancellationToken);
            var byLabel = directory.ToDictionary(p => p.Label, p => p.Id, StringComparer.OrdinalIgnoreCase);

            var ids = new List<Guid>();
            foreach (var label in normalized)
            {
                if (!byLabel.TryGetValue(label, out var id))
                {
                    var person = Person.Create(userId, label);
                    db.People.Add(person);
                    id = person.Id;
                    byLabel[label] = id;
                }
                ids.Add(id);
            }
            return ids;
        }
    }
}
