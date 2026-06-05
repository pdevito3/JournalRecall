using MediatR;
using Microsoft.EntityFrameworkCore;
using QueryKit;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetSessionList
{
    /// <summary>Optional QueryKit filter (e.g. a CreatedAt date range) over each Session's current state.</summary>
    public sealed record Query(string? Filter) : IRequest<IReadOnlyList<SessionListItemDto>>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Query, IReadOnlyList<SessionListItemDto>>
    {
        private const int PreviewLength = 140;

        public async Task<IReadOnlyList<SessionListItemDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = db.Sessions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.Filter))
                // The config maps topics/people/mood query names onto the owned metadata (issue 0011);
                // built-in names like CreatedAt keep working.
                query = query.ApplyQueryKitFilter(request.Filter, SessionQueryKitConfig.Instance);

            // Project to the current-state fields only — the owned Revision history never becomes rows.
            var rows = await query
                .OrderByDescending(s => s.CreatedAt) // newest first
                .Select(s => new
                {
                    s.Id,
                    s.CreatedAt,
                    s.RawDraft,
                    Topics = s.Topics.Select(t => t.Name).ToList(),
                    People = s.People.Select(p => p.Name).ToList(),
                    s.MoodKey,
                    s.MoodCustomValue,
                })
                .ToListAsync(cancellationToken);

            var timeZoneId = await CurrentUserTimeZone(cancellationToken);

            return rows.Select(s => new SessionListItemDto(
                s.Id,
                s.CreatedAt,
                JournalingDay.For(s.CreatedAt, timeZoneId),
                Preview(s.RawDraft),
                s.Topics,
                s.People,
                s.MoodKey is null ? null : new MoodDto(s.MoodKey, s.MoodCustomValue))).ToList();
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
