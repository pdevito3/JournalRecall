namespace JournalRecall.Api.Exceptions;

/// <summary>
/// Thrown when an authenticated user has no roles and so cannot be authorized. Mapped to HTTP 403
/// by the ProblemDetails pipeline (<see cref="Extensions.ProblemDetailsConfigurationExtension"/>).
/// </summary>
public class NoRolesAssignedException : Exception
{
    public NoRolesAssignedException() : base("No roles are assigned to this user.") { }

    public NoRolesAssignedException(string message) : base(message) { }
}
