using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using JournalRecall.AI.Core;
using JournalRecall.AI.Core.Persistence;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// Optional thread orchestration (ADR-0007): load history → run → append, with optimistic
/// concurrency and idempotency-key dedupe. Consumers can use this or call <see cref="IAgentRunner"/>
/// directly with client-supplied history.
/// </summary>
public sealed class ConversationThreadRunner(IAgentRunner runner, IConversationStore store, TimeProvider timeProvider)
{
    /// <summary>
    /// Runs <paramref name="userInput"/> against thread <paramref name="threadId"/>: loads prior
    /// messages, runs the agent, and persists the new user + assistant messages. The
    /// <paramref name="idempotencyKey"/> makes client retries safe.
    /// </summary>
    public async Task<AgentOutcome> RunAsync(
        AgentDefinition definition,
        string threadId,
        string userInput,
        RunContext context,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var thread = await store.LoadAsync(threadId, cancellationToken);

        var priorMessages = thread.Messages.Select(m => m.ToChatMessage()).ToList();
        var conversation = new Conversation
        {
            ThreadId = threadId,
            Messages = [.. priorMessages, new ChatMessage(ChatRole.User, userInput)],
        };

        var outcome = await runner.RunAsync(definition, conversation, context, cancellationToken);

        var transcript = outcome switch
        {
            AgentOutcome.Completed c => c.Messages,
            AgentOutcome.Stopped s => s.Messages,
            _ => null,
        };

        if (transcript is not null)
            await PersistNewTurnsAsync(threadId, priorMessages.Count, transcript, [], null, null, thread.Version, idempotencyKey, cancellationToken);

        return outcome;
    }

    /// <summary>
    /// Streaming counterpart to <see cref="RunAsync"/>: loads history, yields the live event stream,
    /// and persists the new turns when the terminal event arrives — giving durable threads the same
    /// token-by-token streaming the one-shot endpoint has. The append happens <i>before</i> the
    /// terminal event is surfaced, so a client that refetches the thread on completion never races it.
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> StreamAsync(
        AgentDefinition definition,
        string threadId,
        string userInput,
        RunContext context,
        string? idempotencyKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var thread = await store.LoadAsync(threadId, cancellationToken);

        var priorMessages = thread.Messages.Select(m => m.ToChatMessage()).ToList();
        var conversation = new Conversation
        {
            ThreadId = threadId,
            Messages = [.. priorMessages, new ChatMessage(ChatRole.User, userInput)],
        };

        // Fold the non-message events into durable activity as they stream, so the terminal append
        // can attach this turn's tool/resource/delegate/progress trail (with timing + token usage)
        // to the assistant message.
        var activity = new List<StoredActivity>();
        DateTimeOffset? runStart = null;
        long? totalTokens = null;
        await foreach (var @event in runner.StreamAsync(definition, conversation, context, cancellationToken))
        {
            var at = timeProvider.GetUtcNow();
            runStart ??= at;
            if (@event is AgentEvent.UsageUpdated usage)
                totalTokens = usage.TotalTokens;
            Accumulate(activity, @event, at);

            var transcript = @event switch
            {
                AgentEvent.Completed e => e.Outcome.Messages,
                AgentEvent.Stopped e => e.Outcome.Messages,
                _ => null,
            };

            if (transcript is not null)
            {
                var durationMs = runStart is { } start ? (at - start).TotalMilliseconds : (double?)null;
                await PersistNewTurnsAsync(threadId, priorMessages.Count, transcript, activity, totalTokens, durationMs, thread.Version, idempotencyKey, cancellationToken);
            }

            yield return @event;
        }
    }

    /// <summary>Folds one event into the running activity list, merging tool lifecycle into one entry.</summary>
    private static void Accumulate(List<StoredActivity> activity, AgentEvent @event, DateTimeOffset at)
    {
        switch (@event)
        {
            case AgentEvent.ToolInvoking e:
                activity.Add(new StoredActivity { Kind = "tool", Tool = e.ToolName, Status = "invoking", CallId = e.CallId, OccurredAt = at });
                break;
            case AgentEvent.ToolSucceeded e:
                UpdateTool(activity, e.CallId, e.ToolName, "succeeded", null, at);
                break;
            case AgentEvent.ToolFailed e:
                UpdateTool(activity, e.CallId, e.ToolName, "failed", e.Error, at);
                break;
            case AgentEvent.ResourceRead e:
                activity.Add(new StoredActivity { Kind = "resource", Resource = e.ResourceName, OccurredAt = at });
                break;
            case AgentEvent.AgentDelegated e:
                activity.Add(new StoredActivity { Kind = "delegate", Agent = e.AgentName, OccurredAt = at });
                break;
            case AgentEvent.Progress e:
                activity.Add(new StoredActivity { Kind = "progress", Value = e.Value, Total = e.Total, Message = e.Message, OccurredAt = at });
                break;
        }
    }

    /// <summary>Updates the most recent still-invoking tool entry, recording its elapsed duration (mirrors the UI reducer).</summary>
    private static void UpdateTool(List<StoredActivity> activity, string? callId, string toolName, string status, string? error, DateTimeOffset at)
    {
        for (var i = activity.Count - 1; i >= 0; i--)
        {
            var entry = activity[i];
            if (entry.Kind != "tool" || entry.Status != "invoking")
                continue;
            var matches = callId is not null ? entry.CallId == callId : entry.Tool == toolName;
            if (!matches)
                continue;
            var durationMs = entry.OccurredAt is { } started ? (at - started).TotalMilliseconds : (double?)null;
            activity[i] = entry with { Status = status, Error = error, DurationMs = durationMs };
            return;
        }
    }

    /// <summary>
    /// The transcript is (prior + newUser + generated); persist only what's new this run, and only
    /// durable conversational turns (user/assistant text) — not intermediate tool plumbing.
    /// </summary>
    private async Task PersistNewTurnsAsync(
        string threadId,
        int priorCount,
        IReadOnlyList<ChatMessage> transcript,
        IReadOnlyList<StoredActivity> activity,
        long? totalTokens,
        double? durationMs,
        long expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var newMessages = transcript
            .Skip(priorCount)
            .Where(m => (m.Role == ChatRole.User || m.Role == ChatRole.Assistant) && !string.IsNullOrEmpty(m.Text))
            .Select(m => StoredMessage.FromChatMessage(m, now))
            .ToArray();

        // Attach this turn's activity + timing + token usage to the assistant message it produced
        // (the last new one).
        for (var i = newMessages.Length - 1; i >= 0; i--)
        {
            if (newMessages[i].Role == ChatRole.Assistant.Value)
            {
                newMessages[i] = newMessages[i] with
                {
                    Activity = activity.Count > 0 ? [.. activity] : null,
                    TotalTokens = totalTokens,
                    DurationMs = durationMs,
                };
                break;
            }
        }

        if (newMessages.Length > 0)
            await store.AppendAsync(threadId, newMessages, expectedVersion, idempotencyKey, cancellationToken);
    }
}
