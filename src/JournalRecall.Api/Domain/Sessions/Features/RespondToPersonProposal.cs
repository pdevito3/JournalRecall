using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

/// <summary>
/// Approve or reject an AI People-tag proposal (PRD-0006, RICH-009). Reject drops the proposal; approve
/// resolves the target Person (the proposal's exact match, a reassigned existing Person, or a freshly
/// created one) and inserts mention nodes at every occurrence of the proposed name in the Cleaned copy —
/// deterministically, with no further AI rewriting. The People badges then reconcile from the prose.
/// </summary>
public static class RespondToPersonProposal
{
    /// <summary>Returns false when the Session, the proposal, or a reassign target doesn't exist for the user (→ 404).</summary>
    public sealed record Command(Guid SessionId, string Label, bool Approve, Guid? BindToPersonId, bool CreateNew)
        : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db, PeopleTagService peopleTags)
        : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            var proposal = session.PeopleProposals.FirstOrDefault(p => p.Matches(request.Label));
            if (proposal is null)
                return false;

            if (!request.Approve)
            {
                session.RemovePersonProposal(request.Label);
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }

            // A reassign target must be the caller's own directory entry (the global query filter scopes this).
            if (request.BindToPersonId is { } bind && !await db.People.AnyAsync(p => p.Id == bind, cancellationToken))
                return false;

            var cleanedJson = await peopleTags.ApproveAsync(
                session, proposal, request.BindToPersonId, request.CreateNew, cancellationToken);
            session.ApplyCleanedMentions(cleanedJson);
            session.RemovePersonProposal(request.Label);

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
