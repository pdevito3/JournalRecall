using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Domain.People;

/// <summary>
/// The single source of truth for resolving a detected name to a directory <see cref="Person"/>
/// (PRD-0006, RICH-006): given a name, returns the existing <c>PersonId</c> when the directory already
/// holds a match, else signals "new" (<c>null</c>). Repo-backed and per-User — the global query filter
/// scopes the directory to the current User (Privacy invariant). Consumed by the manual @-mention path
/// (RICH-007) and the AI people-tag proposal (RICH-009); it resolves only and never creates.
/// </summary>
/// <remarks>
/// Matching is exact on the <see cref="Person.Label"/>, case-insensitive and trimmed — mirroring the
/// directory's own dedup rule. Aliases are the intended next layer (an alias→Person match would resolve
/// here too), but <see cref="Person"/> carries no alias collection yet (RICH-005 deferred it), so today
/// "exact / alias" collapses to exact. The directory is small and per-User, so it is matched in memory.
/// </remarks>
public sealed class PersonResolver(JournalRecallDbContext db)
{
    /// <summary>
    /// Resolves <paramref name="name"/> to an existing directory <c>PersonId</c>, or <c>null</c> when no
    /// match exists ("new"). A blank name never matches.
    /// </summary>
    public async Task<Guid?> ResolveAsync(string? name, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return null;

        var directory = await db.People.AsNoTracking().ToListAsync(cancellationToken);
        var match = directory.FirstOrDefault(
            p => p.Label.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }
}
