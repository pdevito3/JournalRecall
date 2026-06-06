using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetSession
{
    public sealed record Query(Guid SessionId) : IRequest<SessionDto?>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Query, SessionDto?>
    {
        public async Task<SessionDto?> Handle(Query request, CancellationToken cancellationToken)
        {
            // The global query filter scopes this to the current user; another user's id simply
            // returns nothing (Privacy invariant), which the endpoint surfaces as 404. Project so a
            // read doesn't pull the whole Revision history. Stale is derived in SQL: a Clean Session
            // whose Raw Revision count has advanced past the last cleaned Revision reads as Stale.
            var row = await db.Sessions
                .AsNoTracking()
                .Where(s => s.Id == request.SessionId)
                .Select(s => new
                {
                    s.Id,
                    s.CreatedAt,
                    s.RawDraft,
                    s.CleanedDraft,
                    s.Synopsis,
                    Status = s.CleanupStatus == CleanupStatus.Clean && s.RawRevisions.Count > s.LastCleanedRawRevisionNumber
                        ? CleanupStatus.Stale
                        : s.CleanupStatus,
                    s.CleanedHasHandEdits,
                    Topics = s.Topics.Select(t => t.Name).ToList(),
                    PersonIds = s.People.Select(p => p.PersonId).ToList(),
                    s.MoodKey,
                    s.MoodCustomValue,
                    Suggestions = s.Suggestions
                        .Select(g => new SuggestionDto(g.Kind, g.Value, g.MoodCustomValue)).ToList(),
                    s.Latitude,
                    s.Longitude,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
                return null;

            // People are directory references; resolve their display labels (per-user via the filter).
            var people = await db.People
                .Where(p => row.PersonIds.Contains(p.Id))
                .OrderBy(p => p.Label)
                .Select(p => p.Label)
                .ToListAsync(cancellationToken);

            return new SessionDto(
                row.Id, row.CreatedAt, row.RawDraft, row.CleanedDraft, row.Synopsis, row.Status,
                row.CleanedHasHandEdits, row.Topics, people,
                row.MoodKey is null ? null : new MoodDto(row.MoodKey, row.MoodCustomValue),
                row.Suggestions,
                Location.TryCreate(row.Latitude, row.Longitude, out var location)
                    ? new LocationDto(location.Latitude, location.Longitude)
                    : null);
        }
    }
}
