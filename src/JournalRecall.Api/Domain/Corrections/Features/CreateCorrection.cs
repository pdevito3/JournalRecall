using Mapster;
using MediatR;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Corrections.Features;

public static class CreateCorrection
{
    public sealed record Command(CorrectionForWrite Correction) : IRequest<CorrectionDto>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Command, CorrectionDto>
    {
        public async Task<CorrectionDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

            var correction = Correction.Create(
                userId, request.Correction.CanonicalTerm, request.Correction.Mishearings, request.Correction.HardReplace);
            db.Corrections.Add(correction);
            await db.SaveChangesAsync(cancellationToken);

            return correction.Adapt<CorrectionDto>();
        }
    }
}
