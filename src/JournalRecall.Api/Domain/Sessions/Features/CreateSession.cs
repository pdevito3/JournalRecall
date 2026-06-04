using Mapster;
using MediatR;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class CreateSession
{
    public sealed record Command : IRequest<SessionDto>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Command, SessionDto>
    {
        public async Task<SessionDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

            var session = Session.Create(userId);
            db.Sessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);

            return session.Adapt<SessionDto>();
        }
    }
}
