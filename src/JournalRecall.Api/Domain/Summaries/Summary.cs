namespace JournalRecall.Api.Domain.Summaries;

/// <summary>
/// An AI-generated narrative over a time period for one User (CONTEXT.md), keyed by (User, Period,
/// anchor date). Distinct from a Session's Synopsis. Day and Week summarize the underlying Sessions
/// directly (issue 0013); the Month/Quarter/Year roll-ups and staleness propagation arrive in 0014.
/// Per-user and private via the global query filter (Privacy invariant).
/// </summary>
public sealed class Summary : BaseEntity
{
    public Guid UserId { get; private set; }
    public SummaryPeriod Period { get; private set; }

    /// <summary>The canonical anchor date for the period (a Day's date, or a Week's ISO Monday).</summary>
    public DateOnly PeriodDate { get; private set; }

    /// <summary>The AI-generated narrative. Empty until a generation run succeeds.</summary>
    public string Content { get; private set; } = string.Empty;

    public SummaryStatus Status { get; private set; } = SummaryStatus.Generating;

    /// <summary>How many Sessions fed the latest generation (display + tests).</summary>
    public int SourceSessionCount { get; private set; }

    /// <summary>When the latest successful generation completed; null until then.</summary>
    public DateTimeOffset? GeneratedAt { get; private set; }

    private Summary() { } // EF

    public static Summary Create(Guid userId, SummaryPeriod period, DateOnly periodDate) => new()
    {
        UserId = userId,
        Period = period,
        PeriodDate = periodDate,
    };

    /// <summary>Marks a (re)generation as in flight so a concurrent read sees <see cref="SummaryStatus.Generating"/>.</summary>
    public void BeginGeneration() => Status = SummaryStatus.Generating;

    /// <summary>Folds a successful run into the Summary: the narrative, source count, and a fresh timestamp.</summary>
    public void Complete(string content, int sourceSessionCount)
    {
        Content = content ?? string.Empty;
        SourceSessionCount = sourceSessionCount;
        GeneratedAt = DateTimeOffset.UtcNow;
        Status = SummaryStatus.Ready;
    }

    /// <summary>
    /// Marks a Ready Summary <see cref="SummaryStatus.Stale"/> because something beneath it changed
    /// (issue 0014). A no-op when it is not Ready (Generating/Missing/already Stale), so an in-flight run
    /// is never disturbed and the content is left intact for the regenerate affordance.
    /// </summary>
    public void MarkStale()
    {
        if (Status == SummaryStatus.Ready)
            Status = SummaryStatus.Stale;
    }
}
