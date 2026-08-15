using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace JournalRecall.Api.Observability;

/// <summary>
/// The API's own <see cref="ActivitySource"/> plus the two trace shapes the request pipeline cannot
/// express by itself: a span that starts its own trace (<see cref="TraceRoot"/>) and a trace context
/// that travels in data instead of headers (<see cref="TraceContext"/>).
/// <para>
/// Privacy invariant (CONTEXT.md): a span carries names, identifiers, and counts. It never carries
/// journal content. Every method here takes its tags from the caller, so each call site keeps that rule.
/// </para>
/// </summary>
public static class ApiTelemetry
{
    public const string SourceName = "JournalRecall.Api";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    /// <summary>
    /// Starts a span that is the root of a new trace, and links it back to the activity it displaced.
    /// <para>
    /// Use this for work that outlives, or is far larger than, the request that triggered it - an AI
    /// Cleanup run under a long Server-Sent Events response, for example. As a child, that run would
    /// hide inside a transport span and share a trace with everything else on the same connection. As
    /// a root it is discoverable on its own, and the link still records what caused it.
    /// </para>
    /// The scope restores the previous ambient activity on dispose, so the caller's trace continues.
    /// </summary>
    /// <param name="tags">Span attributes. Metadata only - never journal content.</param>
    /// <param name="traceName">The span name, and therefore the trace name.</param>
    /// <param name="additionalLinks">Further causes to link, such as a context a client sent.</param>
    public static ActivityScope TraceRoot(
        (string Key, object? Value)[] tags,
        string traceName,
        IEnumerable<ActivityLink>? additionalLinks = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // Detach before the span starts. StartActivity parents to Activity.Current, so clearing it
        // first is what makes the new span a root instead of a child.
        var previous = Activity.Current;
        Activity.Current = null;

        var activity = ActivitySource.StartActivity(
            traceName,
            ActivityKind.Internal,
            parentContext: default,
            tags: BuildTags(tags, memberName, filePath, lineNumber),
            links: [.. LinksTo(previous), .. additionalLinks ?? []]);

        // No listener sampled the span, so nothing was started and nothing displaced the ambient
        // activity. Put it back now rather than at dispose.
        if (activity is null)
            Activity.Current = previous;

        return new ActivityScope(activity, previous);
    }

    /// <summary>
    /// Captures the current trace context for a hop that headers cannot cover. Both values are null
    /// when no activity is current, and <see cref="TryParseTraceContext"/> then reports false.
    /// </summary>
    public static TraceContext CaptureCurrentTraceContext() =>
        new(Activity.Current?.Id, Activity.Current?.TraceStateString);

    /// <summary>
    /// Parses a captured <see cref="TraceContext"/> back into an <see cref="ActivityContext"/>.
    /// Returns false for a null, empty, or malformed context, so a caller that receives no context
    /// keeps working with an untraced parent.
    /// </summary>
    public static bool TryParseTraceContext(TraceContext? traceContext, out ActivityContext activityContext)
    {
        activityContext = default;
        if (traceContext is null || string.IsNullOrWhiteSpace(traceContext.TraceParent))
            return false;

        return ActivityContext.TryParse(
            traceContext.TraceParent, traceContext.TraceState, isRemote: true, out activityContext);
    }

    private static List<KeyValuePair<string, object?>> BuildTags(
        (string Key, object? Value)[] tags, string memberName, string filePath, int lineNumber)
    {
        var result = new List<KeyValuePair<string, object?>>(tags.Length + 3);
        foreach (var (key, value) in tags)
            result.Add(new KeyValuePair<string, object?>(key, value));

        // Same attribute names the EF query-tag enrichment writes, so one call-site filter finds both.
        result.Add(new KeyValuePair<string, object?>("code.function", memberName));
        result.Add(new KeyValuePair<string, object?>("code.file.path", filePath));
        result.Add(new KeyValuePair<string, object?>("code.line.number", lineNumber));
        return result;
    }

    private static ActivityLink[] LinksTo(Activity? activity)
    {
        if (activity is null)
            return [];

        var context = activity.Context;
        return context.TraceId != default && context.SpanId != default ? [new ActivityLink(context)] : [];
    }
}
