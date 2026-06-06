using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Integration reference test (PRD-0003, TEST-0003): an AI Cleanup run driven through the real
/// <see cref="SessionCleanupRunner"/> and the scripted chat client, deterministically and without HTTP.
/// Asserts the persisted aggregate state, not implementation.
/// </summary>
public class session_cleanup_tests : TestBase
{
    [Fact]
    public async Task cleanup_produces_a_cleaned_copy_and_synopsis_and_leaves_raw_unchanged()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder()
            .WithUserId(scope.CurrentUserId)
            .WithRawText("helo wrld this is my entry")
            .Build();
        await scope.InsertAsync(session);

        var dto = await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        dto.ShouldNotBeNull();
        dto!.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        // Content is canonical JSON now; its derived plaintext is what carries the words (ADR-0009).
        PlainText(dto.CleanedDraft).ShouldBe("Polished: helo wrld this is my entry"); // from the scripted client
        dto.Synopsis.ShouldNotBeEmpty();
        PlainText(dto.RawDraft).ShouldBe("helo wrld this is my entry"); // Raw is never touched
    }

    [Fact]
    public async Task a_model_failure_records_failed_without_corrupting_raw()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder()
            .WithUserId(scope.CurrentUserId)
            .WithRawText("original words")
            .Build();
        await scope.InsertAsync(session);

        CleanupChat.Throw = true;
        var dto = await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        dto.ShouldNotBeNull();
        dto!.CleanupStatus.ShouldBe(CleanupStatus.Failed);
        PlainText(dto.RawDraft).ShouldBe("original words");
        dto.CleanedDraft.ShouldBeEmpty();
    }
}
