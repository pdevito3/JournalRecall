using Microsoft.EntityFrameworkCore;
using JournalRecall.AI.Core;
using JournalRecall.AI.Runtime;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Summaries.Ai;
using JournalRecall.Api.Domain.Summaries.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Summaries.Services;

/// <summary>
/// Generates (or refreshes) a Summary on demand (issues 0013/0014): it upserts the Summary keyed by
/// (user, period, anchor), marks it Generating, drives the agent over its source material — the period's
/// Session texts for a Day/Week, or the existing lower-level Summaries for a Month/Quarter/Year — and
/// folds the narrative back in as Ready. Completing a run propagates staleness up the chain. No
/// background scheduler. A period with no source material is a no-op reporting <see cref="SummaryStatus.Missing"/>.
/// </summary>
public sealed class SummaryGenerator(
    JournalRecallDbContext db,
    IAgentRunner runner,
    SummarySourceReader sources,
    SummaryRollupReader rollups,
    SummaryStaleness staleness,
    ICurrentUserService currentUser)
{
    public async Task<SummaryDto> GenerateAsync(
        SummaryPeriod period, DateOnly date, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");
        var anchor = SummaryPeriods.Anchor(period, date);

        // Source material: Sessions for Day/Week, else the existing lower-level Summaries.
        var (prompt, sourceCount) = await BuildPrompt(period, anchor, cancellationToken);

        // Nothing to summarize: don't persist an empty Summary (only periods with material get one).
        if (sourceCount == 0)
            return SummaryDto.Missing(period, anchor, 0);

        var summary = await db.Summaries
            .FirstOrDefaultAsync(s => s.Period == period && s.PeriodDate == anchor, cancellationToken);
        if (summary is null)
        {
            summary = Summary.Create(userId, period, anchor);
            db.Summaries.Add(summary);
        }

        summary.BeginGeneration();
        await db.SaveChangesAsync(cancellationToken);

        var content = await Run(prompt, userId, cancellationToken);

        summary.Complete(content, sourceCount);
        await db.SaveChangesAsync(cancellationToken);

        // This period's content changed, so anything that rolls it up is now out of date (issue 0014).
        await staleness.PropagateAboveAsync(period, anchor, cancellationToken);

        return SummaryDto.From(summary, sourceCount);
    }

    private async Task<(string Prompt, int SourceCount)> BuildPrompt(
        SummaryPeriod period, DateOnly anchor, CancellationToken ct)
    {
        if (SummaryPeriods.IsSessionLevel(period))
        {
            var entries = await sources.ReadAsync(period, anchor, ct);
            return (SummaryAgent.BuildPrompt(period, anchor, entries), entries.Count);
        }

        var children = await rollups.ReadAsync(period, anchor, ct);
        return (SummaryAgent.BuildRollupPrompt(period, anchor, children), children.Count);
    }

    private async Task<string> Run(string prompt, Guid userId, CancellationToken ct)
    {
        var definition = SummaryAgent.BuildDefinition();
        var context = new RunContext { Subject = userId.ToString() };

        await foreach (var @event in runner.StreamAsync(definition, Conversation.FromUser(prompt), context, ct))
        {
            var terminal = @event switch
            {
                AgentEvent.Completed c => (AgentOutcome)c.Outcome,
                AgentEvent.Stopped s => s.Outcome,
                AgentEvent.Failed f => f.Outcome,
                _ => null,
            };

            if (terminal is AgentOutcome.Completed completed && SummaryAgent.TryExtract(completed, out var content))
                return content;
        }

        // A model failure, a guardrail stop, or empty output: keep the (empty) narrative rather than throwing.
        return string.Empty;
    }
}
