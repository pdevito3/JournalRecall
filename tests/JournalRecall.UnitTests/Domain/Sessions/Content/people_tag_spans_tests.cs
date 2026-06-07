using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.UnitTests.Domain.Sessions.Content;

/// <summary>
/// Unit tests for <see cref="PeopleTagSpans"/> (PRD-0006, RICH-009): the pure locator finds every
/// word-boundary occurrence of a proposed name in the derived plaintext, builds <see cref="MentionSpan"/>s
/// whose text mirrors the document's own casing (so <see cref="MentionInsertion"/>'s exact check passes),
/// and extracts one sentence preview per distinct occurrence. No host or DB.
/// </summary>
public class people_tag_spans_tests
{
    [Fact]
    public void finds_each_word_boundary_occurrence()
    {
        var text = "Saw Sam today, then Sam left.";
        PeopleTagSpans.Occurrences(text, "Sam").ShouldBe([4, 20]);
    }

    [Fact]
    public void does_not_match_inside_a_longer_word()
    {
        // "Sam" must not match inside "Samuel" or "Samantha".
        PeopleTagSpans.Occurrences("Samuel met Samantha", "Sam").ShouldBeEmpty();
    }

    [Fact]
    public void matching_is_case_insensitive_but_spans_keep_the_documents_casing()
    {
        var personId = Guid.CreateVersion7();
        var spans = PeopleTagSpans.Spans("we saw SAM and sam", "Sam", personId, "Sam");

        spans.Select(s => s.Start).ShouldBe([7, 15]);
        spans.Select(s => s.Text).ShouldBe(["SAM", "sam"]); // the document's own casing, not the needle's
        spans.ShouldAllBe(s => s.PersonId == personId && s.Label == "Sam");
    }

    [Fact]
    public void contexts_are_the_distinct_sentences_around_occurrences()
    {
        var text = "I had lunch with Sam. Later I called Mara. Sam waved back.";
        PeopleTagSpans.Contexts(text, "Sam")
            .ShouldBe(["I had lunch with Sam.", "Sam waved back."]);
    }

    [Fact]
    public void two_occurrences_in_one_sentence_collapse_to_one_context()
    {
        var text = "Sam and Sam are different people somehow.";
        PeopleTagSpans.Contexts(text, "Sam").Count.ShouldBe(1);
    }

    [Fact]
    public void blank_or_absent_names_yield_nothing()
    {
        PeopleTagSpans.Occurrences("anything", " ").ShouldBeEmpty();
        PeopleTagSpans.Occurrences("anything", "Nobody").ShouldBeEmpty();
        PeopleTagSpans.Contexts("anything", "Nobody").ShouldBeEmpty();
        PeopleTagSpans.Spans(null, "Sam", Guid.CreateVersion7(), "Sam").ShouldBeEmpty();
    }
}
