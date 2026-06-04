using Microsoft.EntityFrameworkCore;

namespace JournalRecall.Api.Databases;

/// <summary>
/// The application's single database context (file-based SQLite; ADR-0001). Phase 0 carries no
/// aggregates yet — the Session aggregate and its Revision streams land in Phase 2 (issue 0004) —
/// so the initial migration establishes only the migrations-history table and the .db file.
/// </summary>
public sealed class JournalRecallDbContext(DbContextOptions<JournalRecallDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
