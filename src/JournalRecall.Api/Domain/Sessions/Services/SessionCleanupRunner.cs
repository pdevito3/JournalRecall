using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using JournalRecall.AI.Core;
using JournalRecall.AI.Runtime;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Summaries.Services;

namespace JournalRecall.Api.Domain.Sessions.Services;

/// <summary>
/// Orchestrates an AI Cleanup run for one Session: marks it Running, drives the agent runner over the
/// Raw text, and folds the terminal outcome back into the aggregate (Cleaned copy + Synopsis on
/// success, Failed otherwise) — all without ever touching Raw (issue 0008, ADR-0003/0004). The runner's
/// event stream is surfaced verbatim so the endpoint can stream live progress to the client.
/// </summary>
public sealed class SessionCleanupRunner(JournalRecallDbContext db, IAgentRunner runner, SummaryStaleness staleness)
{
    /// <summary>
    /// Runs Cleanup, yielding each lifecycle event as it occurs. The Session's terminal state is
    /// persisted just before the terminal event is yielded, so a client re-reading the Session the
    /// moment the stream ends sees the final status. Yields nothing if the Session does not exist for
    /// the current user (Privacy invariant via the global query filter).
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> StreamAsync(
        Guid sessionId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
            yield break;

        var rawText = session.RawDraft;
        session.BeginCleanup();
        await db.SaveChangesAsync(cancellationToken);

        // The caller's Corrections (per-user via the global query filter): hint-mode entries go into
        // the prompt; hard-replace entries are substituted deterministically after the model runs.
        var corrections = await db.Corrections.AsNoTracking().ToListAsync(cancellationToken);
        var definition = CleanupAgent.BuildDefinition(CorrectionApplier.BuildHintContext(corrections));
        var context = new RunContext { Subject = session.UserId.ToString() };

        await foreach (var @event in runner.StreamAsync(
            definition, Conversation.FromUser(rawText), context, cancellationToken))
        {
            var terminal = @event switch
            {
                AgentEvent.Completed c => (AgentOutcome)c.Outcome,
                AgentEvent.Stopped s => s.Outcome,
                AgentEvent.Failed f => f.Outcome,
                _ => null,
            };

            if (terminal is not null)
            {
                Apply(session, terminal, corrections);
                await db.SaveChangesAsync(cancellationToken);

                // A successful run rewrote the Cleaned copy, which the period Summaries read — invalidate
                // the day's Summary chain so it offers regeneration (issue 0014; CONTEXT.md).
                if (session.CleanupStatus == CleanupStatus.Clean)
                    await staleness.MarkStaleForSessionAsync(session, cancellationToken);
            }

            yield return @event;
        }
    }

    /// <summary>Runs Cleanup to completion and returns the updated Session, or null when it doesn't exist.</summary>
    public async Task<SessionDto?> RunAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == sessionId, cancellationToken))
            return null;

        await foreach (var _ in StreamAsync(sessionId, cancellationToken)) { }

        // Same scoped DbContext → the tracked, just-updated instance from the EF identity map.
        var session = await db.Sessions.FirstAsync(s => s.Id == sessionId, cancellationToken);
        return SessionDto.From(session);
    }

    private static void Apply(Session session, AgentOutcome outcome, IReadOnlyList<Correction> corrections)
    {
        if (outcome is AgentOutcome.Completed completed && CleanupAgent.TryParse(completed, out var parsed))
        {
            // Hard-replace Corrections are applied deterministically to the Cleaned copy only.
            session.CompleteCleanup(CorrectionApplier.ApplyHardReplacements(parsed.Cleaned, corrections), parsed.Synopsis);
            // The same run proposes metadata Suggestions (issue 0012) — pending until accepted/rejected.
            session.ReplaceAiSuggestions(parsed.Topics, parsed.People, parsed.Mood);
        }
        else
        {
            // A model failure, a guardrail stop, or unparseable output: record the failure. Raw and any
            // prior Cleaned copy are untouched (acceptance criteria).
            session.FailCleanup();
        }
    }
}
