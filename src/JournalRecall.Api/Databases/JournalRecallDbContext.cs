using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Identity;

namespace JournalRecall.Api.Databases;

/// <summary>
/// The application's single database context (file-based SQLite; ADR-0001). Hosts ASP.NET Core
/// Identity (User + roles) from issue 0002; the Session aggregate and its Revision streams land in
/// Phase 2 (issue 0004).
/// </summary>
public sealed class JournalRecallDbContext(DbContextOptions<JournalRecallDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
