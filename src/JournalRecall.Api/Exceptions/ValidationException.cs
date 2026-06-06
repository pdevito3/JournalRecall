namespace JournalRecall.Api.Exceptions;

/// <summary>
/// Thrown when a request fails validation. Surfaces as an RFC 7807 validation problem+json
/// (HTTP 400) carrying a per-field <c>errors</c> map, via the ProblemDetails pipeline
/// (<see cref="Extensions.ProblemDetailsConfigurationExtension"/>).
/// </summary>
public class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures have occurred.")
        => Errors = new Dictionary<string, string[]>();

    public ValidationException(IDictionary<string, string[]> errors) : this()
        => Errors = errors;

    public ValidationException(string property, string error) : this()
        => Errors = new Dictionary<string, string[]> { [property] = [error] };

    public IDictionary<string, string[]> Errors { get; }
}
