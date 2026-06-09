using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class CreateSession
{
    /// <summary>
    /// Optional body: a client-minted Session <c>id</c> so an offline create can be replayed safely
    /// (ADR-0013, issue 0031), and a captured lat/long, sent only when the user has geo opt-in on
    /// (issue 0015).
    /// </summary>
    public sealed record Request(Guid? Id, double? Latitude, double? Longitude);

    public sealed record Command(double? Latitude, double? Longitude, Guid? Id = null) : IRequest<SessionDto?>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser, ISender sender)
        : IRequestHandler<Command, SessionDto?>
    {
        public async Task<SessionDto?> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

            if (request.Id is { } clientId)
            {
                // A client-minted id (ADR-0013): look it up across all users (tenant filter off) so a
                // replay and a cross-user collision are told apart before the insert can blow up on the PK.
                var ownerId = await db.Sessions
                    .IgnoreQueryFilters([JournalRecallDbContext.TenantFilter])
                    .Where(s => s.Id == clientId)
                    .Select(s => (Guid?)s.UserId)
                    .FirstOrDefaultAsync(cancellationToken);

                // Replaying our own create (a dropped response, retried) is an idempotent no-op:
                // answer exactly like a GET of the existing Session — no duplicate, no error.
                if (ownerId == userId)
                    return await sender.Send(new GetSession.Query(clientId), cancellationToken);

                // The id exists under another user: deny exactly like any not-yours resource (the
                // endpoint surfaces null as 404), so a probe can't learn the id exists (Privacy invariant).
                if (ownerId is not null)
                    return null;
            }

            // Privacy/defense-in-depth: only stamp a location when the user has opted in. A point sent
            // while opt-in is off is ignored, so the setting alone governs whether location is stored.
            var optedIn = await db.Users.Where(u => u.Id == userId)
                .Select(u => u.LocationCaptureEnabled).FirstOrDefaultAsync(cancellationToken);
            var location = optedIn && Location.TryCreate(request.Latitude, request.Longitude, out var loc)
                ? loc
                : null;

            var session = Session.Create(userId, location, request.Id);
            db.Sessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);

            // A fresh Session references no People and has no pending proposals yet.
            return SessionDto.From(session, [], []);
        }
    }
}
