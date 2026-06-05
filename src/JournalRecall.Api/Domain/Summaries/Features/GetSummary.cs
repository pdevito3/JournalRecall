using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Summaries.Dtos;
using JournalRecall.Api.Domain.Summaries.Services;

namespace JournalRecall.Api.Domain.Summaries.Features;

/// <summary>
/// Reads the current state of a period Summary (issues 0013/0014). Pure: it never generates. Returns the
/// stored Summary (Ready/Generating/Stale) or a <see cref="SummaryStatus.Missing"/> placeholder, always
/// carrying the live count of source items in the period — Sessions for a Day/Week, lower-level Summaries
/// for a roll-up — so the page can decide whether to trigger a run.
/// </summary>
public static class GetSummary
{
    public sealed record Query(SummaryPeriod Period, DateOnly Date) : IRequest<SummaryDto>;

    public sealed class Handler(JournalRecallDbContext db, SummarySourceReader sources, SummaryRollupReader rollups)
        : IRequestHandler<Query, SummaryDto>
    {
        public async Task<SummaryDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var anchor = SummaryPeriods.Anchor(request.Period, request.Date);

            var sourceCount = SummaryPeriods.IsSessionLevel(request.Period)
                ? (await sources.ReadAsync(request.Period, anchor, cancellationToken)).Count
                : (await rollups.ReadAsync(request.Period, anchor, cancellationToken)).Count;

            var summary = await db.Summaries.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Period == request.Period && s.PeriodDate == anchor, cancellationToken);

            return summary is null
                ? SummaryDto.Missing(request.Period, anchor, sourceCount)
                : SummaryDto.From(summary, sourceCount);
        }
    }
}
