using JournalRecall.Api.Domain.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>
/// The derived-plaintext projection the aggregate writes at every Raw/Cleaned save point (RICH-003,
/// ADR-0009): the JSON markup is stored verbatim while a plain-text rendering is recomputed alongside it,
/// so search and the AI never see the markup. No host, no DB.
/// </summary>
public class session_plaintext_projection_tests
{
    private static Session New() => Session.Create(Guid.CreateVersion7());

    [Fact]
    public void saving_raw_json_derives_its_plaintext_projection()
    {
        var s = New();
        var json = Doc("# Heading\n\n- a **bold** point");

        s.SaveDraft(json);

        s.RawDraft.ShouldBe(json);                  // markup stored verbatim
        s.RawPlainText.ShouldContain("Heading");    // heading marker stripped
        s.RawPlainText.ShouldContain("a bold point"); // list bullet + bold marks stripped
        s.RawPlainText.ShouldNotContain("#");
        s.RawPlainText.ShouldNotContain("*");
    }

    [Fact]
    public void completing_and_hand_editing_cleaned_each_derive_the_plaintext_projection()
    {
        var s = New();
        s.SaveDraft(Doc("raw"));

        s.BeginCleanup();
        s.CompleteCleanup(Doc("polished copy"), "synopsis");
        s.CleanedPlainText.ShouldBe("polished copy");

        s.EditCleaned(Doc("hand edited copy"));
        s.CleanedPlainText.ShouldBe("hand edited copy");
    }

    [Fact]
    public void empty_or_invalid_content_projects_to_empty_plaintext()
    {
        var s = New();

        s.SaveDraft("");
        s.RawPlainText.ShouldBeEmpty();

        s.SaveDraft("not json at all");
        s.RawPlainText.ShouldBeEmpty(); // invalid markup never leaks into the search index
    }
}
