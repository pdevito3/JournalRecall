using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using JournalRecall.AI.Core;
using JournalRecall.AI.Observability;

namespace JournalRecall.AI.Runtime;

/// <summary>
/// The imperative shell. Owns the outer loop: it composes the model pipeline, drives one model call
/// per turn, dispatches the requested tool calls through M.E.AI's <see cref="AIFunction"/> mechanics
/// (gated by the pure <see cref="AgentPolicy"/>), folds results into <see cref="AgentState"/>, emits
/// the <see cref="AgentEvent"/> stream, applies Polly resilience, and produces an
/// <see cref="AgentOutcome"/> (ADR-0001, 0005, 0006).
/// </summary>
internal sealed class AgentRunner(
    IServiceScopeFactory scopeFactory,
    ICapabilityResolver capabilities,
    ResiliencePipelineProvider<string> resiliencePipelines,
    TimeProvider timeProvider,
    IOptions<TelemetryOptions> telemetryOptions,
    ILogger<AgentRunner> logger) : IAgentRunner
{
    private readonly TelemetryOptions _telemetry = telemetryOptions.Value;

    public async Task<AgentOutcome> RunAsync(
        AgentDefinition definition,
        Conversation conversation,
        RunContext context,
        CancellationToken cancellationToken = default)
    {
        AgentOutcome? outcome = null;
        await foreach (var @event in StreamAsync(definition, conversation, context, cancellationToken))
        {
            outcome = @event switch
            {
                AgentEvent.Completed c => c.Outcome,
                AgentEvent.Stopped s => s.Outcome,
                AgentEvent.Failed f => f.Outcome,
                _ => outcome,
            };
        }

        return outcome ?? new AgentOutcome.Failed("Run produced no terminal outcome.");
    }

    public async IAsyncEnumerable<AgentEvent> StreamAsync(
        AgentDefinition definition,
        Conversation conversation,
        RunContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(context);

        using var runActivity = Telemetry.ActivitySource.StartActivity(Telemetry.Spans.Run);
        runActivity?.SetTag(Telemetry.Tags.AgentName, definition.Name);
        runActivity?.SetTag(Telemetry.Tags.CorrelationId, context.CorrelationId);
        logger.LogInformation("Agent {Agent} run started ({CorrelationId})", definition.Name, context.CorrelationId);

        yield return new AgentEvent.RunStarted(definition.Name, context.CorrelationId);

        // A run owns a DI scope so tool/resource instances can use scoped services. The scope lives
        // for the whole loop (its capabilities are invoked across turns) and is disposed when the
        // enumerator completes.
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IRunContextAccessor>().Current = context;

        // Resolve the live model + capabilities for this run. Resolution failures are values, not throws.
        IChatClient? client = null;
        IReadOnlyList<AITool> tools = [];
        IReadOnlyList<ChatMessage> pinned = [];
        var progress = new System.Collections.Concurrent.ConcurrentQueue<AgentEvent>();
        var effectiveDefinition = definition;
        AgentOutcome.Failed? resolutionFailure = null;
        try
        {
            client = WrapWithTelemetry(ResolveChatClient(definition, scope.ServiceProvider));
            using (Telemetry.ActivitySource.StartActivity(
                Telemetry.Spans.ResourceAssembly, ActivityKind.Internal, runActivity?.Context ?? default))
            {
                var materialized = await capabilities.ResolveAsync(definition, context, scope.ServiceProvider, cancellationToken);
                tools = materialized.Tools;
                pinned = materialized.PinnedContext;
                progress = materialized.ProgressEvents;

                // Tools discovered at run time (e.g. MCP) aren't in the pure definition; merge their
                // authorization descriptors so Authorize recognizes the calls (ADR-0003).
                if (materialized.ExtraToolDescriptors.Count > 0)
                    effectiveDefinition = definition with
                    {
                        Tools = [.. definition.Tools, .. materialized.ExtraToolDescriptors],
                    };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve agent {Agent}", definition.Name);
            resolutionFailure = new AgentOutcome.Failed($"Failed to resolve agent '{definition.Name}': {ex.Message}", ex);
        }

        if (resolutionFailure is not null)
        {
            yield return new AgentEvent.Failed(resolutionFailure);
            yield break;
        }

        var pipeline = resiliencePipelines.GetPipeline(ResilienceKeys.Model);
        var now = timeProvider.GetUtcNow();
        var contextCount = (string.IsNullOrWhiteSpace(effectiveDefinition.Instructions) ? 0 : 1) + pinned.Count;
        var state = new AgentState
        {
            Definition = effectiveDefinition,
            Context = context,
            Messages = BuildInitialMessages(effectiveDefinition, pinned, conversation),
            ContextMessageCount = contextCount,
            StartedAt = now,
            Now = now,
        };

        var runContext = runActivity?.Context ?? default;
        while (true)
        {
            var turn = await ExecuteTurnAsync(state, client!, tools, pipeline, progress, runContext, cancellationToken);
            foreach (var @event in turn.Events)
                yield return @event;

            state = turn.State;
            if (turn.Terminal is not null)
            {
                // Surface any progress already delivered, then the terminal event last.
                while (progress.TryDequeue(out var late))
                    yield return late;

                yield return ToTerminalEvent(turn.Terminal);
                RecordTerminal(runActivity, turn.Terminal, state);
                yield break;
            }
        }
    }

    private void RecordTerminal(Activity? runActivity, AgentOutcome outcome, AgentState state)
    {
        var agent = state.Definition.Name;
        runActivity?.SetTag(Telemetry.Tags.TotalTokens, state.TokensUsed);

        var label = outcome switch
        {
            AgentOutcome.Completed => "completed",
            AgentOutcome.Stopped => "stopped",
            _ => "failed",
        };

        switch (outcome)
        {
            case AgentOutcome.Completed:
                runActivity?.SetTag(Telemetry.Tags.Outcome, label);
                logger.LogInformation("Agent {Agent} completed in {Turns} turn(s), {Tokens} tokens",
                    agent, state.Turn, state.TokensUsed);
                break;
            case AgentOutcome.Stopped stopped:
                runActivity?.SetTag(Telemetry.Tags.Outcome, label);
                runActivity?.SetTag(Telemetry.Tags.StopReason, stopped.Reason.ToString());
                logger.LogInformation("Agent {Agent} stopped: {Reason}", agent, stopped.Reason);
                break;
            case AgentOutcome.Failed failed:
                runActivity?.SetTag(Telemetry.Tags.Outcome, label);
                runActivity?.SetTag(Telemetry.Tags.ErrorType, failed.Exception?.GetType().Name ?? "error");
                logger.LogError(failed.Exception, "Agent {Agent} failed: {Reason}", agent, failed.Reason);
                break;
        }

        var agentTag = new KeyValuePair<string, object?>("agent", agent);
        var outcomeTag = new KeyValuePair<string, object?>("outcome", label);
        Telemetry.Metrics.Runs.Add(1, agentTag, outcomeTag);
        Telemetry.Metrics.Tokens.Add(state.TokensUsed, agentTag);
        Telemetry.Metrics.RunDuration.Record(
            (timeProvider.GetUtcNow() - state.StartedAt).TotalMilliseconds, agentTag, outcomeTag);
    }

    private static void DrainProgress(System.Collections.Concurrent.ConcurrentQueue<AgentEvent> progress, List<AgentEvent> events)
    {
        while (progress.TryDequeue(out var @event))
            events.Add(@event);
    }

    private static void RecordToolCall(string tool, string status) =>
        Telemetry.Metrics.ToolCalls.Add(1,
            new KeyValuePair<string, object?>("tool", tool),
            new KeyValuePair<string, object?>("status", status));

    private IChatClient WrapWithTelemetry(IChatClient client) =>
        new ChatClientBuilder(client)
            .UseOpenTelemetry(sourceName: Telemetry.SourceName,
                configure: o => o.EnableSensitiveData = _telemetry.CaptureContent)
            .Build();

    private async Task<TurnResult> ExecuteTurnAsync(
        AgentState state,
        IChatClient client,
        IReadOnlyList<AITool> tools,
        ResiliencePipeline pipeline,
        System.Collections.Concurrent.ConcurrentQueue<AgentEvent> progress,
        ActivityContext parentContext,
        CancellationToken cancellationToken)
    {
        var events = new List<AgentEvent>();
        DrainProgress(progress, events); // any progress that arrived after the previous turn's tool returned
        var turnNumber = state.Turn + 1;
        events.Add(new AgentEvent.TurnStarted(turnNumber));
        Telemetry.Metrics.Turns.Add(1, new KeyValuePair<string, object?>("agent", state.Definition.Name));

        using var turnActivity = Telemetry.ActivitySource.StartActivity(
            Telemetry.Spans.Turn, ActivityKind.Internal, parentContext);
        turnActivity?.SetTag(Telemetry.Tags.Turn, turnNumber);
        var turnContext = turnActivity?.Context ?? parentContext;

        ChatResponse response;
        try
        {
            var options = tools.Count > 0 ? new ChatOptions { Tools = [.. tools] } : null;
            response = await pipeline.ExecuteAsync(
                async token => await client.GetResponseAsync(state.Messages, options, token),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var stopped = new AgentOutcome.Stopped(StopReason.Cancelled, state.Transcript);
            return new TurnResult(events, state with { Cancelled = true }, stopped);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Model call failed for agent {Agent}", state.Definition.Name);
            var failed = new AgentOutcome.Failed($"Model call failed: {ex.Message}", ex);
            return new TurnResult(events, state, failed);
        }

        var messages = new List<ChatMessage>(state.Messages);
        messages.AddRange(response.Messages);

        var tokensUsed = state.TokensUsed + (response.Usage?.TotalTokenCount ?? 0);
        if (response.Usage is not null)
            events.Add(new AgentEvent.UsageUpdated(tokensUsed, response.Usage));

        var calls = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .ToList();

        var retries = new Dictionary<string, int>(state.ToolRetries, StringComparer.Ordinal);

        if (calls.Count > 0)
        {
            var resultContents = new List<AIContent>();
            foreach (var call in calls)
            {
                var policyState = state with { ToolRetries = retries };
                var dispatch = await DispatchToolAsync(call, tools, policyState, events, retries, progress, turnContext, cancellationToken);

                if (dispatch.Terminal is not null)
                {
                    var stateOnFailure = state with { Messages = messages, TokensUsed = tokensUsed, ToolRetries = retries };
                    return new TurnResult(events, stateOnFailure, dispatch.Terminal);
                }

                resultContents.Add(dispatch.Result!);
            }

            messages.Add(new ChatMessage(ChatRole.Tool, resultContents));
        }

        var newState = state with
        {
            Turn = turnNumber,
            Messages = messages,
            TokensUsed = tokensUsed,
            ModelProducedFinalResponse = calls.Count == 0,
            ToolRetries = retries,
            Now = timeProvider.GetUtcNow(),
        };

        return AgentPolicy.Decide(newState) switch
        {
            Decision.Complete => new TurnResult(events, newState,
                new AgentOutcome.Completed(newState.Transcript, response.Usage)),
            Decision.Stop stop => new TurnResult(events, newState,
                new AgentOutcome.Stopped(stop.Reason, newState.Transcript, response.Usage)),
            _ => new TurnResult(events, newState, Terminal: null),
        };
    }

    private async Task<ToolDispatch> DispatchToolAsync(
        FunctionCallContent call,
        IReadOnlyList<AITool> tools,
        AgentState policyState,
        List<AgentEvent> events,
        Dictionary<string, int> retries,
        System.Collections.Concurrent.ConcurrentQueue<AgentEvent> progress,
        ActivityContext parentContext,
        CancellationToken cancellationToken)
    {
        var callId = call.CallId ?? call.Name;
        var invocation = new ToolInvocation(call.Name, callId);

        if (AgentPolicy.Authorize(invocation, policyState) is AuthorizationResult.Denied denied)
        {
            events.Add(new AgentEvent.ToolFailed(call.Name, denied.Reason, callId));
            RecordToolCall(call.Name, "denied");
            return ToolDispatch.FeedBack(new FunctionResultContent(callId, $"Authorization denied: {denied.Reason}"));
        }

        var function = tools.OfType<AIFunction>().FirstOrDefault(f => f.Name == call.Name);
        if (function is null)
        {
            events.Add(new AgentEvent.ToolFailed(call.Name, "Tool not available", callId));
            RecordToolCall(call.Name, "unavailable");
            return ToolDispatch.FeedBack(new FunctionResultContent(callId, "Tool not available."));
        }

        var isDelegation = policyState.Definition.FindSubAgent(call.Name) is not null;
        events.Add(isDelegation
            ? new AgentEvent.AgentDelegated(call.Name)
            : new AgentEvent.ToolInvoking(call.Name, callId));

        using var toolActivity = Telemetry.ActivitySource.StartActivity(
            isDelegation ? Telemetry.Spans.Delegate : Telemetry.Spans.Tool, ActivityKind.Internal, parentContext);
        toolActivity?.SetTag(isDelegation ? Telemetry.Tags.DelegateAgent : Telemetry.Tags.ToolName, call.Name);

        try
        {
            var result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken);
            DrainProgress(progress, events); // surface any progress the tool reported during the call
            events.Add(new AgentEvent.ToolSucceeded(call.Name, callId));
            RecordToolCall(call.Name, "succeeded");
            if (_telemetry.CaptureContent && result is not null)
                toolActivity?.SetTag(Telemetry.Tags.ToolResult, _telemetry.Redactor.Redact(result.ToString() ?? string.Empty));
            return ToolDispatch.FeedBack(new FunctionResultContent(callId, result));
        }
        catch (Exception ex)
        {
            DrainProgress(progress, events);
            events.Add(new AgentEvent.ToolFailed(call.Name, ex.Message, callId));
            RecordToolCall(call.Name, "failed");
            toolActivity?.SetTag(Telemetry.Tags.ErrorType, ex.GetType().Name);
            switch (AgentPolicy.OnToolError(invocation, ex, policyState))
            {
                case ToolErrorDecision.FailRun failRun:
                    return ToolDispatch.Fail(new AgentOutcome.Failed(failRun.Reason, ex));
                case ToolErrorDecision.FeedBack feedBack:
                    retries[call.Name] = policyState.RetriesFor(call.Name) + 1;
                    return ToolDispatch.FeedBack(new FunctionResultContent(callId, feedBack.Message));
                default:
                    return ToolDispatch.Fail(new AgentOutcome.Failed("Unhandled tool-error decision."));
            }
        }
    }

    private static IChatClient ResolveChatClient(AgentDefinition definition, IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(definition.ModelName))
            throw new InvalidOperationException(
                $"Agent '{definition.Name}' has no model. Call UsingModel(...) and register a keyed IChatClient.");

        return services.GetRequiredKeyedService<IChatClient>(definition.ModelName);
    }

    private static List<ChatMessage> BuildInitialMessages(
        AgentDefinition definition,
        IReadOnlyList<ChatMessage> pinned,
        Conversation conversation)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(definition.Instructions))
            messages.Add(new ChatMessage(ChatRole.System, definition.Instructions));
        messages.AddRange(pinned);
        messages.AddRange(conversation.Messages);
        return messages;
    }

    private static AgentEvent ToTerminalEvent(AgentOutcome outcome) => outcome switch
    {
        AgentOutcome.Completed c => new AgentEvent.Completed(c),
        AgentOutcome.Stopped s => new AgentEvent.Stopped(s),
        AgentOutcome.Failed f => new AgentEvent.Failed(f),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private sealed record TurnResult(IReadOnlyList<AgentEvent> Events, AgentState State, AgentOutcome? Terminal);

    private sealed record ToolDispatch(AIContent? Result, AgentOutcome.Failed? Terminal)
    {
        public static ToolDispatch FeedBack(AIContent result) => new(result, null);
        public static ToolDispatch Fail(AgentOutcome.Failed terminal) => new(null, terminal);
    }
}
