using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Users.Features;

public static class UpdateUserSettings
{
    public sealed record Request(string? TimeZoneId);

    /// <summary>Result: Ok, or Invalid when the timezone id can't be resolved (→ 400).</summary>
    public enum Result { Ok, InvalidTimeZone }

    public sealed record Command(string? TimeZoneId) : IRequest<Result>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (!JournalingDay.IsValidTimeZone(request.TimeZoneId))
                return Result.InvalidTimeZone;

            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");
            var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
            user.TimeZoneId = request.TimeZoneId;
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }
    }
}
