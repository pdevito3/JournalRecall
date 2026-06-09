using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Users.Features;

public static class UpdateUserSettings
{
    /// <summary><paramref name="ClientSavedAt"/> is the optional offline-replay save time (ADR-0013,
    /// issue 0032); the web client omits it and behaves exactly as before.</summary>
    public sealed record Request(
        string? TimeZoneId, bool LocationCaptureEnabled, bool RequirePeopleTagApproval,
        DateTimeOffset? ClientSavedAt = null);

    /// <summary>Result: Ok, or Invalid when the timezone id can't be resolved (→ 400).</summary>
    public enum Result { Ok, InvalidTimeZone }

    public sealed record Command(
        string? TimeZoneId, bool LocationCaptureEnabled, bool RequirePeopleTagApproval,
        DateTimeOffset? ClientSavedAt = null)
        : IRequest<Result>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser, TimeProvider timeProvider)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (!JournalingDay.IsValidTimeZone(request.TimeZoneId))
                return Result.InvalidTimeZone;

            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");
            var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);

            // Last-write-wins for queued offline edits (ADR-0013, issue 0032): a settings write the user
            // saved before the last settings save must not clobber newer state — acknowledged, not applied.
            if (request.ClientSavedAt is { } clientSavedAt
                && user.SettingsSavedAt is { } lastSavedAt && clientSavedAt < lastSavedAt)
                return Result.Ok;

            user.TimeZoneId = request.TimeZoneId;
            user.LocationCaptureEnabled = request.LocationCaptureEnabled;
            user.RequirePeopleTagApproval = request.RequirePeopleTagApproval;
            // User isn't a BaseEntity, so the Settings change-feed watermark is stamped here (issue 0033).
            // A skipped (LWW-losing) write above never reaches this stamp, so it can't advance the feed.
            user.SettingsUpdatedAt = timeProvider.GetUtcNow();
            user.SettingsSavedAt = request.ClientSavedAt ?? timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok;
        }
    }
}
