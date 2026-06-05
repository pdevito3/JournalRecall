using Microsoft.EntityFrameworkCore;
using JournalRecall.AI.Core;
using JournalRecall.AI.Runtime;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Summaries.Ai;
using JournalRecall.Api.Domain.Summaries.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Summaries.Services;

/// <summary>
/// Generates (or refreshes) a Day/Week Summary on demand (issue 0013): it upserts the Summary keyed by
/// (user, period, anchor), marks it Generating, drives the agent over the period's Session texts, and
/// folds the narrative back in as Ready. No background scheduler — generation is always user-triggered.
/// A period with no Sessions is a no-op that reports <see cref="SummaryStatus.Missing"/>.
/// </summary>
public sealed class SummaryGenerator(
    JournalRecallDbContext db, IAgentRunner runner, SummarySourceReader sources, ICurrentUserService currentUser)
{
    public async Task<SummaryDto> GenerateAsync(
        SummaryPeriod period, DateOnly date, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");
        var anchor = SummaryPeriods.Anchor(period, date);

        var entries = await sources.ReadAsync(period, anchor, cancellationToken);

        // Nothing to summarize: don't persist an empty Summary (acceptance — only periods with Sessions).
        if (entries.Count == 0)
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

        var content = await Run(period, anchor, entries, userId, cancellationToken);

        summary.Complete(content, entries.Count);
        await db.SaveChangesAsync(cancellationToken);

        return SummaryDto.From(summary, entries.Count);
    }

    private async Task<string> Run(
        SummaryPeriod period, DateOnly anchor, IReadOnlyList<string> entries, Guid userId, CancellationToken ct)
    {
        var definition = SummaryAgent.BuildDefinition();
        var prompt = SummaryAgent.BuildPrompt(period, anchor, entries);
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
