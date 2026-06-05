using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Domain.Corrections.Features;

public static class DeleteCorrection
{
    /// <summary>Returns false when the Correction doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid Id) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var correction = await db.Corrections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (correction is null)
                return false;

            db.Corrections.Remove(correction);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
