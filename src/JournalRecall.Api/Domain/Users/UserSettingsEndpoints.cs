using MediatR;
using JournalRecall.Api.Domain.Users.Features;

namespace JournalRecall.Api.Domain.Users;

public static class UserSettingsEndpoints
{
    public static IEndpointRouteBuilder MapUserSettings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/settings").RequireAuthorization();

        group.MapGet("", async (ISender sender) =>
            Results.Ok(await sender.Send(new GetUserSettings.Query())));

        group.MapPut("", async (UpdateUserSettings.Request body, ISender sender) =>
        {
            var result = await sender.Send(new UpdateUserSettings.Command(body.TimeZoneId, body.LocationCaptureEnabled));
            return result == UpdateUserSettings.Result.Ok
                ? Results.NoContent()
                : Results.Problem("Unknown timezone.", statusCode: StatusCodes.Status400BadRequest);
        });

        return app;
    }
}
