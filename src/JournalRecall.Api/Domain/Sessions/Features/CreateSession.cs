using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class CreateSession
{
    /// <summary>Optional body: a captured lat/long, sent only when the user has geo opt-in on (issue 0015).</summary>
    public sealed record Request(double? Latitude, double? Longitude);

    public sealed record Command(double? Latitude, double? Longitude) : IRequest<SessionDto>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Command, SessionDto>
    {
        public async Task<SessionDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

            // Privacy/defense-in-depth: only stamp a location when the user has opted in. A point sent
            // while opt-in is off is ignored, so the setting alone governs whether location is stored.
            var optedIn = await db.Users.Where(u => u.Id == userId)
                .Select(u => u.LocationCaptureEnabled).FirstOrDefaultAsync(cancellationToken);
            var location = optedIn && Location.TryCreate(request.Latitude, request.Longitude, out var loc)
                ? loc
                : null;

            var session = Session.Create(userId, location);
            db.Sessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);

            // A fresh Session references no People and has no pending proposals yet.
            return SessionDto.From(session, [], []);
        }
    }
}
