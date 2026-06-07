namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// An AI-proposed People tag awaiting the User's per-Person review (PRD-0006, RICH-009). Produced by a
/// Cleanup run when <c>RequirePeopleTagApproval</c> is on, it carries the proposed <see cref="Label"/>
/// (a verbatim name from the Cleaned prose) and, when the directory already holds an exact match, the
/// <see cref="MatchedPersonId"/> to auto-link to — otherwise null, meaning "new." The sentence previews
/// and the spans that approval inserts are derived from the live Cleaned copy (<see cref="Content.PeopleTagSpans"/>),
/// not stored here, so they always reflect the current prose. Part of the Session aggregate (owned).
/// Distinct from the shared <see cref="MetadataSuggestion"/> chip machinery, which People have left.
/// </summary>
public sealed class PersonTagProposal
{
    /// <summary>The proposed display name, taken verbatim from the Cleaned prose.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>The exact directory match to auto-link on approval, or null when the name is "new."</summary>
    public Guid? MatchedPersonId { get; private set; }

    private PersonTagProposal() { } // EF

    internal PersonTagProposal(string label, Guid? matchedPersonId)
    {
        Label = label;
        MatchedPersonId = matchedPersonId;
    }

    internal bool Matches(string label) => Label.Equals(label, StringComparison.OrdinalIgnoreCase);
}
