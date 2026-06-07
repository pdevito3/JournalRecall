using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Users.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Users.Features;

public static class GetUserSettings
{
    public sealed record Query : IRequest<UserSettingsDto>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Query, UserSettingsDto>
    {
        public async Task<UserSettingsDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");
            var settings = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserSettingsDto(u.TimeZoneId, u.LocationCaptureEnabled, u.RequirePeopleTagApproval))
                .FirstAsync(cancellationToken);

            return settings;
        }
    }
}
