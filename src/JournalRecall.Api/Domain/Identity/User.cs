using Microsoft.AspNetCore.Identity;

namespace JournalRecall.Api.Domain.Identity;

/// <summary>
/// The tenant boundary (CONTEXT.md). All journal data belongs to exactly one User and is strictly
/// private to that User. Backed by ASP.NET Core Identity with Guid keys to match the BaseEntity
/// convention so a Session's UserId is a Guid everywhere. Roles (Admin/Member) arrive in issue 0003.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
}
