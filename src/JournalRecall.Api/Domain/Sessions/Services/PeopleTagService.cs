using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.People;
using JournalRecall.Api.Domain.Sessions.Content;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Domain.Sessions.Services;

/// <summary>
/// The shared People-tag machinery for the AI proposal flow (PRD-0006, RICH-009): turning the AI's
/// proposed names into reviewable proposals, inserting approved tags deterministically, and (when the
/// User has opted out of approval) tagging inline at Cleanup time. Repo-backed and per-User — directory
/// lookups go through <see cref="PersonResolver"/> and the global query filter (Privacy invariant). The
/// pure offset work lives in <see cref="PeopleTagSpans"/> / <see cref="MentionInsertion"/>; this layer
/// adds only the directory resolution/creation. Used by both the Cleanup runner and the approve handler.
/// </summary>
public sealed class PeopleTagService(JournalRecallDbContext db, PersonResolver resolver)
{
    /// <summary>
    /// Builds the review proposals for a Cleanup run: each proposed name that actually occurs in the
    /// Cleaned plaintext becomes a proposal, resolved to an existing directory Person (exact match) or
    /// flagged "new." Names with no occurrence are dropped — there would be nowhere to tag them.
    /// </summary>
    public async Task<IReadOnlyList<PersonTagProposal>> BuildProposalsAsync(
        string cleanedPlainText, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var proposals = new List<PersonTagProposal>();
        foreach (var name in Dedupe(names))
        {
            if (PeopleTagSpans.Occurrences(cleanedPlainText, name).Count == 0)
                continue;
            var matched = await resolver.ResolveAsync(name, cancellationToken);
            proposals.Add(new PersonTagProposal(name, matched));
        }
        return proposals;
    }

    /// <summary>
    /// Approval-off path: resolves each proposed name to a directory Person (creating one when none
    /// matches) and inserts their mentions inline into the Cleaned copy, returning the updated JSON. New
    /// People are added to the context (saved by the caller).
    /// </summary>
    public async Task<string> InsertInlineAsync(
        string cleanedJson, IEnumerable<string> names, Guid userId, CancellationToken cancellationToken)
    {
        var plain = ProseMirrorToPlainText.Render(cleanedJson);
        var spans = new List<MentionSpan>();
        foreach (var name in Dedupe(names))
        {
            if (PeopleTagSpans.Occurrences(plain, name).Count == 0)
                continue;
            var personId = await resolver.ResolveAsync(name, cancellationToken) ?? CreatePerson(userId, name);
            spans.AddRange(PeopleTagSpans.Spans(plain, name, personId, name));
        }
        return MentionInsertion.Insert(cleanedJson, spans);
    }

    /// <summary>
    /// Resolves the final Person for an approved proposal — bind to an existing Person, force create-new,
    /// or take the proposal's exact match (creating one when the name was "new") — then wraps every
    /// occurrence of the proposed label in the Cleaned copy in a mention, returning the updated JSON. New
    /// People are added to the context (saved by the caller). Assumes <paramref name="bindToPersonId"/>,
    /// when given, has already been validated as the caller's own directory entry.
    /// </summary>
    public async Task<string> ApproveAsync(
        Session session, PersonTagProposal proposal, Guid? bindToPersonId, bool createNew, CancellationToken cancellationToken)
    {
        Guid personId;
        string label;
        if (bindToPersonId is Guid bind)
        {
            personId = bind;
            label = await db.People.Where(p => p.Id == bind).Select(p => p.Label).FirstAsync(cancellationToken);
        }
        else if (createNew || proposal.MatchedPersonId is null)
        {
            personId = CreatePerson(session.UserId, proposal.Label);
            label = proposal.Label.Trim();
        }
        else
        {
            personId = proposal.MatchedPersonId.Value;
            label = await db.People.Where(p => p.Id == personId).Select(p => p.Label).FirstAsync(cancellationToken);
        }

        var spans = PeopleTagSpans.Spans(session.CleanedPlainText, proposal.Label, personId, label);
        return MentionInsertion.Insert(session.CleanedDraft, spans);
    }

    /// <summary>Adds a new directory Person and returns its id (persisted by the caller's SaveChanges).</summary>
    private Guid CreatePerson(Guid userId, string label)
    {
        var person = Person.Create(userId, label);
        db.People.Add(person);
        return person.Id;
    }

    private static IEnumerable<string> Dedupe(IEnumerable<string> names) => (names ?? [])
        .Select(n => n?.Trim() ?? string.Empty)
        .Where(n => n.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
