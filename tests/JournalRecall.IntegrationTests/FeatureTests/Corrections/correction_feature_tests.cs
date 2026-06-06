using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Domain.Corrections.Features;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.IntegrationTests.FeatureTests.Corrections;

/// <summary>
/// Corrections (issue 0009) at the integration layer: per-User CRUD with strict isolation, plus their
/// effect on Cleanup — hint-mode entries reach the prompt, hard-replace entries are substituted
/// deterministically, and neither ever alters Raw. Driven through the MediatR slices + the runner.
/// </summary>
public class correction_feature_tests : TestBase
{
    private static CorrectionForWrite Write(string canonical, string[] mishearings, bool hardReplace) =>
        new(canonical, mishearings, hardReplace);

    [Fact]
    public async Task a_user_can_create_edit_and_delete_their_own_corrections()
    {
        using var scope = new TestingServiceScope();

        var created = await scope.SendAsync(new CreateCorrection.Command(Write("Profisee", ["prophecy", "professionally"], false)));
        created.CanonicalTerm.ShouldBe("Profisee");
        created.Mishearings.ShouldBe(["prophecy", "professionally"]);
        created.HardReplace.ShouldBeFalse();
        (await scope.SendAsync(new GetCorrections.Query())).Count.ShouldBe(1);

        // Edit: flip to hard-replace and change mishearings.
        (await scope.SendAsync(new UpdateCorrection.Command(created.Id, Write("Profisee", ["prophecy"], true)))).ShouldBeTrue();
        var edited = (await scope.SendAsync(new GetCorrections.Query())).Single();
        edited.HardReplace.ShouldBeTrue();
        edited.Mishearings.ShouldBe(["prophecy"]);

        // Delete.
        (await scope.SendAsync(new DeleteCorrection.Command(created.Id))).ShouldBeTrue();
        (await scope.SendAsync(new GetCorrections.Query())).ShouldBeEmpty();
    }

    [Fact]
    public async Task another_users_corrections_are_invisible_and_untouchable()
    {
        using var alice = new TestingServiceScope();
        var correction = await alice.SendAsync(new CreateCorrection.Command(Write("Profisee", ["prophecy"], false)));

        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new GetCorrections.Query())).ShouldBeEmpty();
        (await bob.SendAsync(new UpdateCorrection.Command(correction.Id, Write("X", [], false)))).ShouldBeFalse();
        (await bob.SendAsync(new DeleteCorrection.Command(correction.Id))).ShouldBeFalse();

        (await alice.SendAsync(new GetCorrections.Query())).Count.ShouldBe(1);
    }

    [Fact]
    public async Task hint_mode_correction_is_injected_into_the_cleanup_prompt_and_reflected_in_cleaned()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreateCorrection.Command(Write("Profisee", ["prophecy"], hardReplace: false)));
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId)
            .WithRawText("we evaluated prophecy for our data").Build();
        await scope.InsertAsync(session);

        // Simulate an obedient model honoring the hint in its Cleaned output.
        CleanupChat.CleanedOverride = "We evaluated Profisee for our data.";
        var result = await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        // The Corrections list reached the model as prompt context (hint mode).
        CleanupChat.LastSystemText.ShouldContain("Profisee");
        CleanupChat.LastSystemText.ShouldContain("prophecy");

        PlainText(result!.CleanedDraft).ShouldBe("We evaluated Profisee for our data.");
        PlainText(result.RawDraft).ShouldBe("we evaluated prophecy for our data"); // Raw untouched
    }

    [Fact]
    public async Task hard_replace_correction_substitutes_every_occurrence_in_cleaned_only()
    {
        using var scope = new TestingServiceScope();
        await scope.SendAsync(new CreateCorrection.Command(Write("Profisee", ["prophecy"], hardReplace: true)));
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId)
            .WithRawText("met the prophecy team about prophecy").Build();
        await scope.InsertAsync(session);

        // The fake echoes Raw verbatim as the Cleaned copy; the deterministic hard-replace pass then runs.
        var result = await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        PlainText(result!.CleanedDraft).ShouldBe("Polished: met the Profisee team about Profisee");
        result.CleanedDraft.ShouldNotContain("prophecy");
        PlainText(result.RawDraft).ShouldBe("met the prophecy team about prophecy"); // Raw untouched

        // Hard-replace entries are handled deterministically, not pushed into the prompt as a hint.
        CleanupChat.LastSystemText.ShouldNotContain("commonly misheard");
    }
}
