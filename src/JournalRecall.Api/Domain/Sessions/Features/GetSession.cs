using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Content;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Metadata;

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
                    s.CleanedRegenerationRevisionNumber,
                    Topics = s.Topics.Select(t => t.Name).ToList(),
                    PersonIds = s.People.Select(p => p.PersonId).ToList(),
                    // Read the Moods JSON column as a whole (no element enumeration → no json_each/APPLY on SQLite).
                    Moods = s.Moods,
                    // The single Activity, projected from its complex-type `activity` column (PRD-0007).
                    Activity = s.Activity.Value,
                    Suggestions = s.Suggestions
                        .Select(g => new SuggestionDto(g.Kind, g.Value)).ToList(),
                    // Pending People-tag proposals (RICH-009); previews are derived from the Cleaned prose below.
                    Proposals = s.PeopleProposals
                        .Select(p => new { p.Label, p.MatchedPersonId }).ToList(),
                    s.Latitude,
                    s.Longitude,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
                return null;

            // People are directory references; resolve their display labels (per-user via the filter) — both
            // the mentioned People (badges) and any directory match a proposal would auto-link to.
            var matchedIds = row.Proposals.Where(p => p.MatchedPersonId is not null)
                .Select(p => p.MatchedPersonId!.Value);
            var labels = await db.People
                .Where(p => row.PersonIds.Contains(p.Id) || matchedIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Label })
                .ToListAsync(cancellationToken);

            var people = labels.Where(p => row.PersonIds.Contains(p.Id))
                .OrderBy(p => p.Label).Select(p => p.Label).ToList();
            var directoryLabels = labels.ToDictionary(p => p.Id, p => p.Label);
            var cleanedPlainText = ProseMirrorToPlainText.Render(row.CleanedDraft);
            var proposals = row.Proposals
                .Select(p => PersonTagProposalDto.From(new PersonTagProposal(p.Label, p.MatchedPersonId), cleanedPlainText, directoryLabels))
                .ToList();

            return new SessionDto(
                row.Id, row.CreatedAt, row.RawDraft, row.CleanedDraft, row.Synopsis, row.Status,
                row.CleanedHasHandEdits, row.CleanedRegenerationRevisionNumber, row.Topics, people, row.Moods,
                row.Activity, row.Suggestions, proposals,
                Location.TryCreate(row.Latitude, row.Longitude, out var location)
                    ? new LocationDto(location.Latitude, location.Longitude)
                    : null);
        }
    }
}
