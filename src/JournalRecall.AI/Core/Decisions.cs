namespace JournalRecall.AI.Core;

/// <summary>A model-requested tool invocation, reduced to what the pure policy needs.</summary>
public sealed record ToolInvocation(string ToolName, string? CallId = null);

/// <summary>What the outer loop should do next, decided purely from <see cref="AgentState"/>.</summary>
public abstract record Decision
{
    private Decision() { }

    /// <summary>Run another turn.</summary>
    public sealed record Continue : Decision;

    /// <summary>The model produced a final answer; finish with <see cref="AgentOutcome.Completed"/>.</summary>
    public sealed record Complete : Decision;

    /// <summary>A per-run guardrail tripped; finish with <see cref="AgentOutcome.Stopped"/>.</summary>
    public sealed record Stop(StopReason Reason) : Decision;

    public static readonly Decision ContinueRun = new Continue();
    public static readonly Decision CompleteRun = new Complete();
}

/// <summary>Result of authorizing a tool/delegation call against the caller's scopes.</summary>
public abstract record AuthorizationResult
{
    private AuthorizationResult() { }

    public sealed record Allowed : AuthorizationResult;

    public sealed record Denied(string Reason) : AuthorizationResult;

    public static readonly AuthorizationResult Allow = new Allowed();

    public bool IsAllowed => this is Allowed;
}

/// <summary>How the policy handles a thrown tool error (ADR-0006).</summary>
public abstract record ToolErrorDecision
{
    private ToolErrorDecision() { }

    /// <summary>Feed the error back to the model as a tool result so it can self-correct.</summary>
    public sealed record FeedBack(string Message) : ToolErrorDecision;

    /// <summary>Terminal: fail the whole run.</summary>
    public sealed record FailRun(string Reason) : ToolErrorDecision;
}
