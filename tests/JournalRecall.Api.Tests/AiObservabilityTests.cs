using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JournalRecall.AI.Observability;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// AI-lifecycle observability over a real Cleanup run (issue 0017). Drives the actual
/// <c>SessionCleanupRunner</c> → <c>IAgentRunner</c> path through the in-memory OpenTelemetry exporter
/// the factory wires onto the real tracing pipeline, then asserts the exported spans. Content capture
/// is off by default for this intimate-journal domain, so the raw journal text must never appear on
/// any exported span.
/// </summary>
public class AiObservabilityTests : IClassFixture<CleanupWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CleanupWebApplicationFactory _factory;

    public AiObservabilityTests(CleanupWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Chat.Throw = false;
        _factory.Chat.CleanedOverride = null;
        _factory.Chat.Synopsis = "A short recap of the session.";
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id, string RawDraft, string CleanedDraft, string CleanupStatus);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private async Task<Guid> RunCleanup(HttpClient client, string rawText)
    {
        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText })).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/sessions/{id}/cleanup", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return id;
    }

    [Fact]
    public async Task Cleanup_run_emits_a_run_span_with_model_token_and_latency_metadata()
    {
        var client = await SignedInClient();
        _factory.ExportedActivities.Clear();

        await RunCleanup(client, "today I felt grateful and a little tired");

        _factory.ExportedActivities.ShouldContain(a =>
            a.OperationName == JournalRecall.AI.Observability.Telemetry.Spans.Run);
        var runSpan = _factory.ExportedActivities.First(a =>
            a.OperationName == JournalRecall.AI.Observability.Telemetry.Spans.Run);

        // Model + token metadata are attached; latency is the span's own measured duration.
        runSpan.GetTagItem(JournalRecall.AI.Observability.Telemetry.Tags.Model)
            .ShouldBe(JournalRecall.Api.Domain.Sessions.Ai.CleanupAgent.ModelKey);
        runSpan.GetTagItem(JournalRecall.AI.Observability.Telemetry.Tags.TotalTokens).ShouldNotBeNull();
        runSpan.GetTagItem(JournalRecall.AI.Observability.Telemetry.Tags.Outcome).ShouldBe("completed");
        runSpan.GetTagItem(JournalRecall.AI.Observability.Telemetry.Tags.CorrelationId).ShouldNotBeNull();
        runSpan.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task No_journal_content_is_exported_when_capture_is_off()
    {
        var client = await SignedInClient();
        _factory.ExportedActivities.Clear();

        // A distinctive phrase that would only appear if raw/cleaned content leaked onto a span.
        const string secret = "zzqx-private-journal-marker-7f3a";
        await RunCleanup(client, $"my secret is {secret} and I told no one");

        // Capture is off by default → no span tag value anywhere carries the journal text.
        foreach (var activity in _factory.ExportedActivities)
        {
            activity.DisplayName.ShouldNotContain(secret);
            foreach (var tag in activity.Tags)
                (tag.Value ?? string.Empty).ShouldNotContain(secret);
        }
    }
}
