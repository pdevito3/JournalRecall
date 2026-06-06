using System.Text.Json.Nodes;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;
using static JournalRecall.SharedTestHelpers.Fakes.Sessions.ContentDoc;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// The canonical ProseMirror/tiptap JSON content model (RICH-003, ADR-0009) at the integration layer:
/// Raw/Cleaned persist as JSON, a derived plaintext projection is written on every save, and that
/// projection — not the JSON markup — drives the timeline preview and word search, so formatting never
/// hides content. Driven through the MediatR slices + the real cleanup runner, no HTTP.
/// </summary>
public class prosemirror_content_tests : TestBase
{
    private static async Task<Guid> NewSession(TestingServiceScope scope) =>
        (await scope.SendAsync(new CreateSession.Command(null, null))).Id;

    [Fact]
    public async Task saving_json_round_trips_the_markup_and_derives_plaintext_for_preview()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        var json = Doc("# Trip to Rome\n\n- saw the **Colosseum**");

        await scope.SendAsync(new SaveDraft.Command(id, json));

        // The editor's JSON is stored and read back byte-for-byte (the wire contract is unchanged).
        (await scope.SendAsync(new GetSession.Query(id)))!.RawDraft.ShouldBe(json);

        // The timeline preview is the derived plaintext — the heading marker and JSON braces are gone.
        var preview = (await scope.SendAsync(new GetSessionList.Query(null))).Single(i => i.Id == id).Preview;
        preview.ShouldContain("Trip to Rome");
        preview.ShouldContain("Colosseum");
        preview.ShouldNotContain("#");
        preview.ShouldNotContain("{");
    }

    [Fact]
    public async Task search_finds_words_buried_in_formatting_via_the_plaintext_index()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        // "Colosseum" lives inside a bold mark inside a list item — pure markup, yet still searchable.
        await scope.SendAsync(new SaveDraft.Command(id, Doc("# Trip to Rome\n\n- saw the **Colosseum**")));

        // Case-insensitive contains against the `raw` plaintext query name.
        var hits = await scope.SendAsync(new GetSessionList.Query("raw @=* \"colosseum\""));
        hits.Select(i => i.Id).ShouldContain(id);

        var misses = await scope.SendAsync(new GetSessionList.Query("raw @=* \"venice\""));
        misses.Select(i => i.Id).ShouldNotContain(id);
    }

    [Fact]
    public async Task cleanup_persists_the_cleaned_copy_as_canonical_json()
    {
        using var scope = new TestingServiceScope();
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText("helo wrld").Build();
        await scope.InsertAsync(session);

        var dto = await scope.GetService<SessionCleanupRunner>().RunAsync(session.Id);

        // The model's plain output is wrapped to canonical JSON so the Cleaned editor renders formatting
        // (ADR-0009) — the stored copy is a parseable ProseMirror document, not raw text.
        var root = JsonNode.Parse(dto!.CleanedDraft);
        root!["type"]!.GetValue<string>().ShouldBe("doc");
        PlainText(dto.CleanedDraft).ShouldBe("Polished: helo wrld");
    }
}
