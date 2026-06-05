using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Features;

/// <summary>
/// Accept or reject a pending AI metadata Suggestion (issue 0012). Accept promotes it to metadata
/// (provenance AiSuggested, never overwriting UserSet); reject discards it.
/// </summary>
public static class RespondToSuggestion
{
    /// <summary>Returns false when the Session or the Suggestion doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid SessionId, SuggestionKind Kind, string Value, bool Accept) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            var handled = request.Accept
                ? session.AcceptSuggestion(request.Kind, request.Value)
                : session.RejectSuggestion(request.Kind, request.Value);
            if (!handled)
                return false;

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
