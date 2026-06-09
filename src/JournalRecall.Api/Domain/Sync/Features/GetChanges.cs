using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Content;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Domain.Sync.Dtos;
using JournalRecall.Api.Domain.Users.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Sync.Features;

public static class GetChanges
{
    /// <summary>
    /// One pull of the delta change feed (issue 0033, ADR-0013). A null/empty <paramref name="Since"/> is
    /// the first-sync bootstrap (full state); otherwise only entities whose <c>UpdatedAt</c> is after the
    /// cursor are returned. Returns null when the cursor isn't one this server issued (→ 400).
    /// </summary>
    public sealed record Query(string? Since) : IRequest<SyncChangesDto?>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Query, SyncChangesDto?>
    {
        public async Task<SyncChangesDto?> Handle(Query request, CancellationToken cancellationToken)
        {
            var bootstrap = string.IsNullOrEmpty(request.Since);
            long sinceTicks = 0;
            if (!bootstrap && !SyncCursor.TryDecode(request.Since!, out sinceTicks))
                return null;

            // The exclusive lower bound: a bootstrap scans from the beginning (zero instant). UpdatedAt
            // persists as UTC ticks, so the comparison is a plain long comparison in SQL.
            var since = new DateTimeOffset(sinceTicks, TimeSpan.Zero);
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

            // Changed Sessions as their full current state, projected like GetSession so the owned
            // Revision history never becomes rows. Every query here is scoped to the caller by the
            // global tenant filter (Privacy invariant); Stale is derived in SQL.
            var sessionRows = await db.Sessions
                .AsNoTracking()
                .Where(s => s.UpdatedAt > since)
                .OrderBy(s => s.UpdatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.CreatedAt,
                    s.UpdatedAt,
                    s.RawDraft,
                    s.CleanedDraft,
                    s.Synopsis,
                    Status = s.CleanupStatus == CleanupStatus.Clean && s.RawRevisions.Count > s.LastCleanedRawRevisionNumber
                        ? CleanupStatus.Stale
                        : s.CleanupStatus,
                    s.CleanedHasHandEdits,
                    s.CleanedRegenerationRevisionNumber,
                    Topics = s.Topics.Select(t => t.Name).ToList(),
                    PersonIds = s.People.Select(p => p.PersonId).ToList(),
                    // Read the Moods JSON column as a whole (no element enumeration → no json_each/APPLY on SQLite).
                    Moods = s.Moods,
                    Activity = s.Activity.Value,
                    Suggestions = s.Suggestions
                        .Select(g => new SuggestionDto(g.Kind, g.Value)).ToList(),
                    Proposals = s.PeopleProposals
                        .Select(p => new { p.Label, p.MatchedPersonId }).ToList(),
                    s.Latitude,
                    s.Longitude,
                })
                .ToListAsync(cancellationToken);

            var corrections = await db.Corrections
                .AsNoTracking()
                .Where(c => c.UpdatedAt > since)
                .OrderBy(c => c.UpdatedAt)
                .ToListAsync(cancellationToken);

            var people = await db.People
                .AsNoTracking()
                .Where(p => p.UpdatedAt > since)
                .OrderBy(p => p.UpdatedAt)
                .Select(p => new { p.Id, p.Label, p.UpdatedAt })
                .ToListAsync(cancellationToken);

            // Settings live on the User row (not a BaseEntity) and carry their own watermark; a bootstrap
            // always includes them, a delta pull only when they changed after the cursor.
            var user = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.TimeZoneId, u.LocationCaptureEnabled, u.RequirePeopleTagApproval, u.SettingsUpdatedAt,
                })
                .FirstOrDefaultAsync(cancellationToken);
            var settings = user is not null && (bootstrap || user.SettingsUpdatedAt > since)
                ? new UserSettingsDto(user.TimeZoneId, user.LocationCaptureEnabled, user.RequirePeopleTagApproval)
                : null;

            // People badges + proposal auto-links are directory references; resolve every needed label
            // across all changed Sessions in one query (per-user via the filter).
            var neededIds = sessionRows.SelectMany(s => s.PersonIds)
                .Concat(sessionRows.SelectMany(s => s.Proposals)
                    .Where(p => p.MatchedPersonId is not null).Select(p => p.MatchedPersonId!.Value))
                .Distinct().ToList();
            var labels = await db.People
                .Where(p => neededIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Label, cancellationToken);

            var sessions = sessionRows.Select(row =>
            {
                var cleanedPlainText = ProseMirrorToPlainText.Render(row.CleanedDraft);
                return new SessionDto(
                    row.Id, row.CreatedAt, row.RawDraft, row.CleanedDraft, row.Synopsis, row.Status,
                    row.CleanedHasHandEdits, row.CleanedRegenerationRevisionNumber, row.Topics,
                    row.PersonIds.Where(labels.ContainsKey).Select(id => labels[id]).OrderBy(l => l).ToList(),
                    row.Moods, row.Activity, row.Suggestions,
                    row.Proposals
                        .Select(p => PersonTagProposalDto.From(
                            new PersonTagProposal(p.Label, p.MatchedPersonId), cleanedPlainText, labels))
                        .ToList(),
                    Location.TryCreate(row.Latitude, row.Longitude, out var location)
                        ? new LocationDto(location.Latitude, location.Longitude)
                        : null);
            }).ToList();

            // The next cursor: the highest watermark this pull observed, never less than the request's —
            // monotonic, and replaying it yields exactly the writes made after it (sequential writes).
            var cursorTicks = sessionRows.Select(s => s.UpdatedAt.UtcTicks)
                .Concat(corrections.Select(c => c.UpdatedAt.UtcTicks))
                .Concat(people.Select(p => p.UpdatedAt.UtcTicks))
                .Append(user?.SettingsUpdatedAt.UtcTicks ?? 0)
                .Append(sinceTicks)
                .Max();

            return new SyncChangesDto(
                SyncCursor.Encode(cursorTicks),
                sessions,
                corrections.Adapt<List<CorrectionDto>>(),
                people.Select(p => new PersonDto(p.Id, p.Label)).ToList(),
                settings);
        }
    }
}
