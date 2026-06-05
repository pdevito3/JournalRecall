using System.Globalization;
using MediatR;
using JournalRecall.Api.Domain.Summaries.Features;
using JournalRecall.Api.Domain.Summaries.Services;

namespace JournalRecall.Api.Domain.Summaries;

public static class SummaryEndpoints
{
    public static IEndpointRouteBuilder MapSummaries(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/summaries").RequireAuthorization();

        // Current state of a period's Summary (Missing until generated). Pure read.
        group.MapGet("/{period}/{date}", async (string period, string date, ISender sender) =>
        {
            if (!TryParse(period, date, out var p, out var d))
                return Results.Problem("Unknown period or date.", statusCode: StatusCodes.Status400BadRequest);

            return Results.Ok(await sender.Send(new GetSummary.Query(p, d)));
        });

        // Generate (or Refresh) the period's Summary on demand, running to completion (issue 0013).
        group.MapPost("/{period}/{date}/generate", async (
            string period, string date, SummaryGenerator generator, CancellationToken ct) =>
        {
            if (!TryParse(period, date, out var p, out var d))
                return Results.Problem("Unknown period or date.", statusCode: StatusCodes.Status400BadRequest);

            return Results.Ok(await generator.GenerateAsync(p, d, ct));
        });

        return app;
    }

    /// <summary>Parses the route's period name (Day/Week today) and ISO date; false on anything unsupported.</summary>
    private static bool TryParse(string period, string date, out SummaryPeriod parsedPeriod, out DateOnly parsedDate)
    {
        parsedDate = default;
        return Enum.TryParse(period, ignoreCase: true, out parsedPeriod)
            && SummaryPeriods.IsSupported(parsedPeriod)
            && DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);
    }
}
