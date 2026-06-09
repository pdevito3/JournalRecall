using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections;
using JournalRecall.Api.Domain.Sessions.Ai;
using JournalRecall.Api.Domain.Sessions.Content;

namespace JournalRecall.Api.Domain.Sessions.Services;

/// <summary>
/// The Engine-independent half of a successful Cleanup (CONTEXT.md "Engine"): folds a parsed
/// CleanupAgent output into the Session the same way regardless of where the model ran — the server
/// (<see cref="SessionCleanupRunner"/>) or the user's device (issue 0034). Hard-replace Corrections are
/// substituted deterministically, the Markdown prose becomes canonical ProseMirror JSON (ADR-0009), and
/// People are parked as proposals or tagged inline per the user's approval setting (RICH-009), so the
/// persisted outcome is indistinguishable across Engines.
/// </summary>
public sealed class CleanupPostProcessor(JournalRecallDbContext db, PeopleTagService peopleTags)
{
    /// <summary>
    /// Completes a Cleanup whose model output parsed successfully: the caller has already called
    /// <see cref="Session.BeginCleanup"/> with the Raw Revision the run read; this hands the aggregate
    /// its whole terminal state. The caller persists (SaveChanges) and invalidates the period Summaries.
    /// </summary>
    public async Task CompleteAsync(
        Session session, CleanupAgent.Parsed parsed, IReadOnlyList<Correction> corrections,
        CancellationToken cancellationToken)
    {
        // Hard-replace Corrections are applied deterministically to the Cleaned copy only. By contract
        // (RICH-004) the model returns Markdown prose; the server converts it to canonical ProseMirror
        // JSON so the Cleaned editor renders with formatting (ADR-0009).
        var cleanedMarkdown = CorrectionApplier.ApplyHardReplacements(parsed.CleanedMarkdown, corrections);
        var cleanedJson = MarkdownToProseMirror.ConvertToJson(cleanedMarkdown);

        // People-tag handling (RICH-009): by default the run proposes People for per-Person review; a User
        // who has turned approval off has resolved mentions tagged inline at Cleanup time. The setting
        // defaults to requiring approval so the AI never writes to the directory without the User's say-so.
        var requireApproval = await db.Users.AsNoTracking()
            .Where(u => u.Id == session.UserId)
            .Select(u => u.RequirePeopleTagApproval)
            .FirstAsync(cancellationToken);

        // Each branch hands the aggregate the data for one whole terminal state (Suggestions always
        // accompany the run, issue 0012); the Session owns the proposal-vs-inline invariant from there.
        if (requireApproval)
        {
            var cleanedPlainText = ProseMirrorToPlainText.Render(cleanedJson);
            var proposals = await peopleTags.BuildProposalsAsync(cleanedPlainText, parsed.PeopleProposal, cancellationToken);
            session.CompleteCleanupWithProposals(
                cleanedJson, parsed.Synopsis, parsed.TopicSuggestions, parsed.MoodSuggestions, proposals);
        }
        else
        {
            cleanedJson = await peopleTags.InsertInlineAsync(cleanedJson, parsed.PeopleProposal, session.UserId, cancellationToken);
            session.CompleteCleanupWithInlineMentions(
                cleanedJson, parsed.Synopsis, parsed.TopicSuggestions, parsed.MoodSuggestions);
        }
    }
}
