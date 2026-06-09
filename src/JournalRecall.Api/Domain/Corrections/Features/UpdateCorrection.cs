using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections.Dtos;

namespace JournalRecall.Api.Domain.Corrections.Features;

public static class UpdateCorrection
{
    /// <summary>Returns false when the Correction doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid Id, CorrectionForWrite Correction) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            // The global query filter scopes this to the current user; another user's row is invisible.
            var correction = await db.Corrections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (correction is null)
                return false;

            // Last-write-wins for queued offline edits (ADR-0013, issue 0032): an update the user saved
            // before this Correction's last write must not clobber newer server state — acknowledged,
            // not applied. The web client never sends ClientSavedAt and always applies, as before.
            if (request.Correction.ClientSavedAt is { } clientSavedAt && clientSavedAt < correction.UpdatedAt)
                return true;

            correction.Update(request.Correction.CanonicalTerm, request.Correction.Mishearings, request.Correction.HardReplace);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
