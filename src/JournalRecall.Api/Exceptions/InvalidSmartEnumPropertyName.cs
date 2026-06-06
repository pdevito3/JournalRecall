namespace JournalRecall.Api.Exceptions;

/// <summary>
/// Thrown when a string can't be resolved to one of a SmartEnum's allowed values — a semantic
/// (not a syntactic) error, so it surfaces as HTTP 422 via the ProblemDetails pipeline
/// (<see cref="Extensions.ProblemDetailsConfigurationExtension"/>). The message lists the allowed values.
/// </summary>
public sealed class InvalidSmartEnumPropertyName : Exception
{
    public InvalidSmartEnumPropertyName(string propertyName, string? providedValue, IEnumerable<string> allowedValues)
        : base($"'{providedValue}' is not a valid {propertyName}. Allowed values: {string.Join(", ", allowedValues)}.")
    {
        PropertyName = propertyName;
        ProvidedValue = providedValue;
    }

    public string PropertyName { get; }

    public string? ProvidedValue { get; }
}
