using JournalRecall.Api.Domain.Sessions.Content;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>
/// An AI People-tag proposal as the review card sees it (PRD-0006, RICH-009): the proposed
/// <see cref="Label"/>, whether it is <see cref="IsNew"/> (no directory match) or auto-links to an
/// existing Person (<see cref="MatchedPersonId"/>/<see cref="MatchedLabel"/>), and the
/// <see cref="Contexts"/> — every sentence the tag would land in, derived from the current Cleaned prose.
/// </summary>
public sealed record PersonTagProposalDto(
    string Label,
    Guid? MatchedPersonId,
    string? MatchedLabel,
    bool IsNew,
    IReadOnlyList<string> Contexts)
{
    /// <summary>
    /// Projects a stored proposal for display: resolves its match label from <paramref name="directoryLabels"/>
    /// and computes its context previews against the current Cleaned plaintext.
    /// </summary>
    public static PersonTagProposalDto From(
        PersonTagProposal proposal, string cleanedPlainText, IReadOnlyDictionary<Guid, string> directoryLabels) => new(
        proposal.Label,
        proposal.MatchedPersonId,
        proposal.MatchedPersonId is { } id && directoryLabels.TryGetValue(id, out var label) ? label : null,
        proposal.MatchedPersonId is null,
        PeopleTagSpans.Contexts(cleanedPlainText, proposal.Label));
}

/// <summary>
/// The per-Person review decision (RICH-009): <see cref="Approve"/> false rejects; otherwise the tag is
/// inserted, binding to <see cref="BindToPersonId"/> (reassign to an existing Person), or forcing
/// <see cref="CreateNew"/>, or — when neither is set — taking the proposal's exact match (or creating one
/// when it was "new").
/// </summary>
public sealed record PersonTagDecision(string Label, bool Approve, Guid? BindToPersonId, bool CreateNew);
