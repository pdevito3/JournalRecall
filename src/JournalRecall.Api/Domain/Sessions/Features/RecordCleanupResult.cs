using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Sessions.Metadata;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.Api.Domain.Summaries.Services;
using JournalRecall.Api.Exceptions;

namespace JournalRecall.Api.Domain.Sessions.Features;

/// <summary>
/// The server half of the OnDevice Engine (issue 0034, ADR-0013): records a Cleanup the user's device
/// ran, post-processing identically to a server run (<see cref="CleanupPostProcessor"/>) — in the domain
/// the outcome is indistinguishable (CONTEXT.md "Engine"). The submitted base Raw Revision pins
/// <c>LastCleanedRawRevisionNumber</c>, so Stale derives naturally when Raw has been edited since.
/// </summary>
public static class RecordCleanupResult
{
    /// <summary>
    /// The CleanupAgent output shape the device assembled, plus the Raw Revision it cleaned against and
    /// the Engine that ran it. The Engine identifier is required but not persisted — the recorded outcome
    /// carries no trace of where the model executed.
    /// </summary>
    public sealed record Request(
        string? CleanedMarkdown,
        string? Synopsis,
        string[]? TopicSuggestions,
        string[]? PeopleProposal,
        string[]? MoodSuggestions,
        int BaseRawRevisionNumber,
        string? Engine);

    /// <summary>Returns false when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid SessionId, Request Result) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db, CleanupPostProcessor postProcessor, SummaryStaleness staleness)
        : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = request.Result;
            Validate(result);

            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            // The device can only have cleaned a Raw Revision the server knows (draft saves sync before
            // queued results upload, ADR-0013). An *older* base is fine — Stale then derives naturally.
            if (result.BaseRawRevisionNumber > session.LatestRawRevisionNumber)
                throw new ValidationException("baseRawRevisionNumber",
                    $"Raw Revision {result.BaseRawRevisionNumber} does not exist — the Session's latest is {session.LatestRawRevisionNumber}.");

            // Resolve the proposed moods leniently, exactly as CleanupAgent.TryParse does (blanks skipped).
            var moods = new List<Mood>();
            foreach (var key in result.MoodSuggestions ?? [])
                if (Mood.TryResolve(key, out var mood))
                    moods.Add(mood);

            var parsed = new CleanupAgent.Parsed(
                result.CleanedMarkdown!,
                result.Synopsis ?? string.Empty,
                result.TopicSuggestions ?? [],
                result.PeopleProposal ?? [],
                moods);

            // Identical post-processing to a server run — including hard-replace Corrections, which are
            // re-applied server-side even if the device missed them — pinned to the device's base Revision.
            var corrections = await db.Corrections.AsNoTracking().ToListAsync(cancellationToken);
            session.BeginCleanup(result.BaseRawRevisionNumber);
            await postProcessor.CompleteAsync(session, parsed, corrections, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            // The recorded result rewrote the Cleaned copy, which the period Summaries read — invalidate
            // the day's Summary chain so it offers regeneration (issue 0014; CONTEXT.md).
            await staleness.MarkStaleForSessionAsync(session, cancellationToken);
            return true;
        }

        /// <summary>
        /// Shape validation, before the Session is loaded or touched: an invalid payload fails with a
        /// validation error and leaves the Session exactly as it was (acceptance criteria).
        /// </summary>
        private static void Validate(Request result)
        {
            var errors = new Dictionary<string, string[]>();

            // Empty string is a legal Cleaned copy (empty Raw cleans to empty); only null/absent is invalid.
            if (result.CleanedMarkdown is null)
                errors["cleanedMarkdown"] = ["cleanedMarkdown is required."];
            if (result.BaseRawRevisionNumber < 1)
                errors["baseRawRevisionNumber"] = ["baseRawRevisionNumber must be at least 1."];
            if (string.IsNullOrWhiteSpace(result.Engine))
                errors["engine"] = ["engine is required."];

            if (errors.Count > 0)
                throw new ValidationException(errors);
        }
    }
}
