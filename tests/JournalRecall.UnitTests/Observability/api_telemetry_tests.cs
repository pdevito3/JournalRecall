using System.Diagnostics;
using JournalRecall.Api.Observability;

namespace JournalRecall.UnitTests.Observability;

/// <summary>
/// Pure test: the trace primitives on their own, with a process-local <see cref="ActivityListener"/>
/// standing in for the exporter. Covers the two properties the whole model rests on — TraceRoot starts
/// a new trace and puts the caller's trace back, and a captured context parses back into the same ids.
/// </summary>
public class api_telemetry_tests : IDisposable
{
    private readonly List<Activity> _stopped = [];
    private readonly ActivityListener _listener;

    public api_telemetry_tests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApiTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Add,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void trace_root_starts_a_new_trace_and_links_back_to_the_caller()
    {
        using var caller = ApiTelemetry.ActivitySource.StartActivity("caller");
        caller.ShouldNotBeNull();

        using (var scope = ApiTelemetry.TraceRoot([("session.id", "s-1")], "sessions.cleanup.run"))
        {
            var root = scope.Activity.ShouldNotBeNull();
            root.Parent.ShouldBeNull();
            root.TraceId.ShouldNotBe(caller.TraceId);
            root.Links.ShouldHaveSingleItem().Context.SpanId.ShouldBe(caller.SpanId);
            root.GetTagItem("session.id").ShouldBe("s-1");
            // The call site travels on the root span under the same names the query tags use.
            root.GetTagItem("code.function").ShouldBe(nameof(trace_root_starts_a_new_trace_and_links_back_to_the_caller));
            (root.GetTagItem("code.file.path") as string).ShouldEndWith("api_telemetry_tests.cs");

            Activity.Current.ShouldBe(root);
        }

        // The caller's trace continues after the scope ends.
        Activity.Current.ShouldBe(caller);
        _stopped.ShouldContain(a => a.DisplayName == "sessions.cleanup.run");
    }

    [Fact]
    public void trace_root_accepts_additional_links()
    {
        using var other = ApiTelemetry.ActivitySource.StartActivity("device.run");
        var extra = new ActivityLink(other!.Context);

        using var scope = ApiTelemetry.TraceRoot([], "sessions.cleanup.run", [extra]);

        scope.Activity.ShouldNotBeNull().Links.ShouldContain(l => l.Context.SpanId == other.SpanId);
    }

    [Fact]
    public void a_captured_context_parses_back_into_the_same_ids()
    {
        using var activity = ApiTelemetry.ActivitySource.StartActivity("producer");
        activity.ShouldNotBeNull();

        var captured = ApiTelemetry.CaptureCurrentTraceContext();

        ApiTelemetry.TryParseTraceContext(captured, out var parsed).ShouldBeTrue();
        parsed.TraceId.ShouldBe(activity.TraceId);
        parsed.SpanId.ShouldBe(activity.SpanId);
        parsed.IsRemote.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("not-a-traceparent", null)]
    public void an_absent_or_malformed_context_reports_false(string? traceParent, string? traceState)
    {
        ApiTelemetry.TryParseTraceContext(new TraceContext(traceParent, traceState), out var parsed).ShouldBeFalse();
        parsed.ShouldBe(default(ActivityContext));
    }

    [Fact]
    public void a_null_context_reports_false()
    {
        ApiTelemetry.TryParseTraceContext(null, out _).ShouldBeFalse();
    }
}
