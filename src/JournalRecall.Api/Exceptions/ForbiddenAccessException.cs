namespace JournalRecall.Api.Exceptions;

/// <summary>
/// Thrown when the caller is not permitted to access a resource. Mapped to HTTP 401 by the
/// ProblemDetails pipeline (<see cref="Extensions.ProblemDetailsConfigurationExtension"/>).
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("You are not authorized to access this resource.") { }

    public ForbiddenAccessException(string message) : base(message) { }
}
