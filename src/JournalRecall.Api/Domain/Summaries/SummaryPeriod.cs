using System.Text.Json.Serialization;

namespace JournalRecall.Api.Domain.Summaries;

/// <summary>
/// The time period a <see cref="Summary"/> covers (CONTEXT.md). Day and Week summarize the underlying
/// Sessions directly; Month/Quarter/Year roll up the level below (issue 0014). Serialized by name so
/// the client reads stable strings rather than ordinals.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SummaryPeriod>))]
public enum SummaryPeriod
{
    /// <summary>A single journaling day.</summary>
    Day,

    /// <summary>An ISO-8601 week (Monday–Sunday); may span a month boundary.</summary>
    Week,

    /// <summary>A calendar month (issue 0014).</summary>
    Month,

    /// <summary>A calendar quarter (issue 0014).</summary>
    Quarter,

    /// <summary>A calendar year (issue 0014).</summary>
    Year,
}
