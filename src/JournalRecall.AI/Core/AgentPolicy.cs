namespace JournalRecall.AI.Core;

/// <summary>
/// The functional core: pure decision logic the shell folds over between turns. No I/O, no mocks
/// needed to test it (ADR-0001). Termination, authorization, and tool-error policy live here.
/// </summary>
public static class AgentPolicy
{
    /// <summary>
    /// Decides what the outer loop does next. A completed model answer wins over guardrails (we got
    /// an answer); guardrails only stop us from starting <i>another</i> turn. Cancellation is checked
    /// first (ADR-0006).
    /// </summary>
    public static Decision Decide(AgentState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Cancelled)
            return new Decision.Stop(StopReason.Cancelled);

        if (state.ModelProducedFinalResponse)
            return Decision.CompleteRun;

        if (state.EffectiveDeadline is { } deadline && state.Now >= deadline)
            return new Decision.Stop(StopReason.Duration);

        if (state.Definition.TokenBudget is { } budget && state.TokensUsed >= budget)
            return new Decision.Stop(StopReason.Budget);

        if (state.Definition.MaxTurns is { } maxTurns && state.Turn >= maxTurns)
            return new Decision.Stop(StopReason.MaxTurns);

        return Decision.ContinueRun;
    }

    /// <summary>
    /// Authorizes a tool or delegation call against the caller's scopes. Unknown tools are denied;
    /// a tool with no declared scope is unrestricted. One mechanism covers tools and delegation
    /// (ADR-0003).
    /// </summary>
    public static AuthorizationResult Authorize(ToolInvocation call, AgentState state)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(state);

        var definition = state.Definition;
        var context = state.Context;

        var tool = definition.FindTool(call.ToolName);
        if (tool is not null)
            return context.HasScope(tool.Scope)
                ? AuthorizationResult.Allow
                : new AuthorizationResult.Denied($"Missing scope '{tool.Scope}' for tool '{call.ToolName}'.");

        var subAgent = definition.FindSubAgent(call.ToolName);
        if (subAgent is not null)
            return context.HasScope(subAgent.Scope)
                ? AuthorizationResult.Allow
                : new AuthorizationResult.Denied($"Missing scope '{subAgent.Scope}' for sub-agent '{call.ToolName}'.");

        return new AuthorizationResult.Denied($"Unknown tool '{call.ToolName}'.");
    }

    /// <summary>
    /// Decides what to do when a tool throws: feed the error back for self-correction (bounded by the
    /// tool's retry count) or fail the run. Fail-closed tools fail immediately (ADR-0006).
    /// </summary>
    public static ToolErrorDecision OnToolError(ToolInvocation call, Exception error, AgentState state)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(state);

        var tool = state.Definition.FindTool(call.ToolName);

        if (tool is null)
            return new ToolErrorDecision.FailRun($"Unknown tool '{call.ToolName}' raised an error.");

        if (tool.FailClosed)
            return new ToolErrorDecision.FailRun(
                $"Tool '{call.ToolName}' is fail-closed and raised: {error.Message}");

        if (state.RetriesFor(call.ToolName) < tool.MaxRetries)
            return new ToolErrorDecision.FeedBack(
                $"Tool '{call.ToolName}' failed: {error.Message}");

        return new ToolErrorDecision.FailRun(
            $"Tool '{call.ToolName}' exhausted {tool.MaxRetries} retries; last error: {error.Message}");
    }
}
