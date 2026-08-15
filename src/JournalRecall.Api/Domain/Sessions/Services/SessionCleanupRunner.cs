using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using JournalRecall.AI.Core;
using JournalRecall.AI.Runtime;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Summaries.Services;
using JournalRecall.Api.Observability;

namespace JournalRecall.Api.Domain.Sessions.Services;

/// <summary>
/// Orchestrates an AI Cleanup run for one Session: marks it Running, drives the agent runner over the
/// Raw text, and folds the terminal outcome back into the aggregate (Cleaned copy + Synopsis on
/// success, Failed otherwise) — all without ever touching Raw (issue 0008, ADR-0003/0004). The runner's
/// event stream is surfaced verbatim so the endpoint can stream live progress to the client.
/// </summary>
public sealed class SessionCleanupRunner(
    JournalRecallDbContext db, IAgentRunner runner, SummaryStaleness staleness, CleanupPostProcessor postProcessor)
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
        // A Cleanup run is its own workflow, not a detail of the request that started it: the streaming
        // endpoint holds one Server-Sent Events response open for the whole run, so as a child every
        // model turn and query would hide under that transport span. TraceRoot gives the run its own
        // trace and links it back to the request.
        using var trace = ApiTelemetry.TraceRoot([("session.id", sessionId)], "sessions.cleanup.run");

        var session = await db.Sessions
            .TagWithOperationCallSite("sessions.cleanup.load")
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
            yield break;

        // Feed the AI the derived plaintext, never the ProseMirror JSON markup (ADR-0009).
        var rawText = session.RawPlainText;
        session.BeginCleanup();
        await db.SaveChangesAsync(cancellationToken);

        // The caller's Corrections (per-user via the global query filter): hint-mode entries go into
        // the prompt; hard-replace entries are substituted deterministically after the model runs.
        var corrections = await db.Corrections.AsNoTracking()
            .TagWithOperationCallSite("sessions.cleanup.corrections")
            .ToListAsync(cancellationToken);
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
                await ApplyAsync(session, terminal, corrections, cancellationToken);
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
        if (!await db.Sessions
            .TagWithOperationCallSite("sessions.cleanup.exists")
            .AnyAsync(s => s.Id == sessionId, cancellationToken))
            return null;

        await foreach (var _ in StreamAsync(sessionId, cancellationToken)) { }

        // Same scoped DbContext → the tracked, just-updated instance from the EF identity map.
        var session = await db.Sessions.FirstAsync(s => s.Id == sessionId, cancellationToken);
        var peopleLabels = await ResolvePeopleLabelsAsync(session, cancellationToken);
        var proposals = await ResolveProposalDtosAsync(session, cancellationToken);
        return SessionDto.From(session, peopleLabels, proposals);
    }

    /// <summary>Projects a Session's pending People-tag proposals for display (match labels + context previews).</summary>
    private async Task<IReadOnlyList<PersonTagProposalDto>> ResolveProposalDtosAsync(
        Session session, CancellationToken cancellationToken)
    {
        if (session.PeopleProposals.Count == 0)
            return [];

        var matchedIds = session.PeopleProposals
            .Where(p => p.MatchedPersonId is not null)
            .Select(p => p.MatchedPersonId!.Value)
            .Distinct()
            .ToList();
        var labels = matchedIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.People
                .TagWithOperationCallSite("sessions.cleanup.matched_person_labels")
                .Where(p => matchedIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Label, cancellationToken);

        return session.PeopleProposals
            .Select(p => PersonTagProposalDto.From(p, session.CleanedPlainText, labels))
            .ToList();
    }

    /// <summary>Resolves a Session's People references to their directory labels (per-user), sorted for display.</summary>
    private async Task<IReadOnlyList<string>> ResolvePeopleLabelsAsync(Session session, CancellationToken cancellationToken)
    {
        var personIds = session.People.Select(p => p.PersonId).ToList();
        if (personIds.Count == 0)
            return [];

        return await db.People
            .TagWithOperationCallSite("sessions.cleanup.person_labels")
            .Where(p => personIds.Contains(p.Id))
            .OrderBy(p => p.Label)
            .Select(p => p.Label)
            .ToListAsync(cancellationToken);
    }

    private async Task ApplyAsync(
        Session session, AgentOutcome outcome, IReadOnlyList<Correction> corrections, CancellationToken cancellationToken)
    {
        if (outcome is not AgentOutcome.Completed completed || !CleanupAgent.TryParse(completed, out var parsed))
        {
            // A model failure, a guardrail stop, or unparseable output: record the failure. Raw and any
            // prior Cleaned copy are untouched (acceptance criteria).
            session.FailCleanup();
            return;
        }

        // The Engine-independent post-processing (hard-replace Corrections, Markdown → ProseMirror,
        // People proposals-vs-inline) is shared with the OnDevice result path (issue 0034).
        await postProcessor.CompleteAsync(session, parsed, corrections, cancellationToken);
    }
}
