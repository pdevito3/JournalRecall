namespace JournalRecall.Api.Domain.Identity;

/// <summary>
/// The two User roles (CONTEXT.md). <see cref="Member"/> is the default for new registrations and
/// grants full use of one's own journal; <see cref="Admin"/> grants only the non-journal admin
/// surface — never any visibility into another User's journal (the Privacy invariant).
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static readonly IReadOnlyList<string> All = [Admin, Member];
}
