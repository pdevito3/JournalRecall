using System.Text.Json.Serialization;

namespace JournalRecall.Api.Domain.Summaries;

/// <summary>
/// Where a period <see cref="Summary"/> stands (CONTEXT.md). <see cref="Missing"/> is not stored — it is
/// the response shape for a period that has no Summary yet. <see cref="Stale"/> (something beneath the
/// Summary changed) drives the regenerate affordance in issue 0014. Serialized by name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SummaryStatus>))]
public enum SummaryStatus
{
    /// <summary>No Summary exists for this period yet — a response-only state, never persisted.</summary>
    Missing,

    /// <summary>A generation run is in progress.</summary>
    Generating,

    /// <summary>The Summary is current with the Sessions (or lower Summaries) it covers.</summary>
    Ready,

    /// <summary>Something beneath the Summary changed since it was generated — regeneration is offered (issue 0014).</summary>
    Stale,
}
