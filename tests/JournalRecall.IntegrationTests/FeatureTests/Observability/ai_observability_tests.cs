using JournalRecall.AI.Observability;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Observability;

/// <summary>
/// AI-lifecycle observability over a real Cleanup run (issue 0017) at the integration layer: drives the
/// actual <see cref="SessionCleanupRunner"/> → IAgentRunner path through the in-memory OpenTelemetry
/// exporter wired onto the real tracing pipeline, then asserts the exported spans. Content capture is off
/// by default for this intimate-journal domain, so the raw journal text must never appear on any span.
/// </summary>
public class ai_observability_tests : TestBase
{
    private static List<System.Diagnostics.Activity> Exported => TestFixture.ExportedActivities;

    private async Task RunCleanup(TestingServiceScope scope, string rawText)
    {
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText(rawText).Build();
        await scope.InsertAsync(session);
        await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);
    }

    [Fact]
    public async Task cleanup_run_emits_a_run_span_with_model_token_and_latency_metadata()
    {
        using var scope = new TestingServiceScope();
        Exported.Clear();

        await RunCleanup(scope, "today I felt grateful and a little tired");

        Exported.ShouldContain(a => a.OperationName == Telemetry.Spans.Run);
        var runSpan = Exported.First(a => a.OperationName == Telemetry.Spans.Run);

        runSpan.GetTagItem(Telemetry.Tags.Model).ShouldBe(CleanupAgent.ModelKey);
        runSpan.GetTagItem(Telemetry.Tags.TotalTokens).ShouldNotBeNull();
        runSpan.GetTagItem(Telemetry.Tags.Outcome).ShouldBe("completed");
        runSpan.GetTagItem(Telemetry.Tags.CorrelationId).ShouldNotBeNull();
        runSpan.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task no_journal_content_is_exported_when_capture_is_off()
    {
        using var scope = new TestingServiceScope();
        Exported.Clear();

        // A distinctive phrase that would only appear if raw/cleaned content leaked onto a span.
        const string secret = "zzqx-private-journal-marker-7f3a";
        await RunCleanup(scope, $"my secret is {secret} and I told no one");

        foreach (var activity in Exported)
        {
            activity.DisplayName.ShouldNotContain(secret);
            foreach (var tag in activity.Tags)
                (tag.Value ?? string.Empty).ShouldNotContain(secret);
        }
    }
}
