using MediatR;
using Microsoft.EntityFrameworkCore;
using QueryKit;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetSessionList
{
    /// <summary>
    /// Optional QueryKit <paramref name="Filter"/> (e.g. topics, raw text, a CreatedAt range) plus an
    /// optional <paramref name="Mood"/> match — a Session matches when any of its Moods equals it. Mood is
    /// separate because it's a JSON collection QueryKit can't express on SQLite.
    /// </summary>
    public sealed record Query(string? Filter, string? Mood = null) : IRequest<IReadOnlyList<SessionListItemDto>>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Query, IReadOnlyList<SessionListItemDto>>
    {
        private const int PreviewLength = 140;

        public async Task<IReadOnlyList<SessionListItemDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = db.Sessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.Filter))
                // The config maps topics/raw query names onto the metadata (issue 0011); built-in names
                // like CreatedAt keep working.
                query = query.ApplyQueryKitFilter(request.Filter, SessionQueryKitConfig.Instance);

            // Mood is a JSON primitive collection: match any element with a translatable EXISTS/contains
            // (QueryKit can't express this on SQLite). The value is resolved so "joyful" matches "Joyful".
            if (!string.IsNullOrWhiteSpace(request.Mood))
            {
                var mood = Metadata.Mood.Resolve(request.Mood).Value;
                query = query.Where(s => s.Moods.Contains(mood));
            }

            // Project to the current-state fields only — the owned Revision history never becomes rows.
            var rows = await query
                .OrderByDescending(s => s.CreatedAt) // newest first
                .Select(s => new
                {
                    s.Id,
                    s.CreatedAt,
                    s.RawPlainText,
                    Topics = s.Topics.Select(t => t.Name).ToList(),
                    PersonIds = s.People.Select(p => p.PersonId).ToList(),
                    // Read the Moods JSON column as a whole (no element enumeration → no json_each/APPLY on SQLite).
                    Moods = s.Moods,
                })
                .ToListAsync(cancellationToken);

            var timeZoneId = await CurrentUserTimeZone(cancellationToken);

            // People are directory references; resolve labels in one query (per-user via the filter).
            var allPersonIds = rows.SelectMany(s => s.PersonIds).Distinct().ToList();
            var labels = await db.People
                .Where(p => allPersonIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Label, cancellationToken);

            return rows.Select(s => new SessionListItemDto(
                s.Id,
                s.CreatedAt,
                JournalingDay.For(s.CreatedAt, timeZoneId),
                Preview(s.RawPlainText),
                s.Topics,
                s.PersonIds.Where(labels.ContainsKey).Select(id => labels[id]).OrderBy(l => l).ToList(),
                s.Moods)).ToList();
        }

        private async Task<string?> CurrentUserTimeZone(CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId;
            if (userId is null)
                return null;

            return await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string Preview(string raw)
        {
            var collapsed = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return collapsed.Length <= PreviewLength ? collapsed : collapsed[..PreviewLength] + "…";
        }
    }
}
